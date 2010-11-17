﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace Dicom2Volume
{
    public class ImageData
    {
        public int Rows;
        public int Columns;
        public double Width;
        public double Height;
        public double WindowWidth;
        public double WindowCenter;
        public double RescaleIntercept;
        public double RescaleSlope;
        public double[] ImageOrientationPatient;
        public double[] ImagePositionPatient;
        public double SliceLocation;
        public int MinIntensity = int.MaxValue;
        public int MaxIntensity = int.MinValue;
        public byte[] PixelData;
    }

    public class VolumeData
    {
        public int Rows;
        public int Columns;
        public int Slices;
        public double Width;
        public double Height;
        public double Depth;
        public double WindowWidth;
        public double WindowCenter;
        public double RescaleIntercept;
        public double RescaleSlope;
        public double[] ImageOrientationPatient;
        public double[] ImagePositionPatient;
        public double FirstSliceLocation;
        public double LastSliceLocation;
        public int MinIntensity = int.MaxValue;
        public int MaxIntensity = int.MinValue;
    }

    public class ImageDataInfo
    {
        public double SliceLocation;
        public string Filename;
    }

    public struct Vector
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public void Cross(Vector v1, Vector v2)
        {
            X = (v1.Y * v2.Z) - (v1.Z * v2.Y);
            Y = -(v1.X * v2.Z) - (v1.Z * v2.X);
            Z = (v1.X * v2.Y) - (v1.Z * v2.X);
        }
    }

    public class Slices
    {
        public static List<string> ConvertDicom(string outputDirectory, params string[] filenames)
        {
            Directory.CreateDirectory(outputDirectory);
            var sliceFilenames = new List<string>();

            for (var i = 0; i < filenames.Length; i++)
            {
                try
                {
                    var inputFilename = filenames[i];
                    var outputFilename = String.Format(Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(inputFilename) ?? ".") + ".xml", i);
                    var imageData = ConvertDicom(inputFilename, outputFilename);
                    if (imageData == null)
                    {
                        Logger.Warn("Unable to convert DICOM slice! " + inputFilename);
                        continue;
                    }

                    sliceFilenames.Add(outputFilename);
                }
                catch (Exception err)
                {
                    Logger.Error(err.ToString());
                }
            }

            return sliceFilenames;
        }

        public static ImageData ConvertDicom(string inputFilename, string outputFilename)
        {
            ImageData imageData = null;
            using (var reader = new BinaryReader(File.OpenRead(inputFilename)))
            {
                var dataset = Dicom.ReadFile(reader);
                if (dataset.Count > 0)
                {
                    if (!VerifyDataFormat(dataset))
                    {
                        Logger.Warn("Not valid dataformat for " + Path.GetFileName(inputFilename) + ". Skipping file..");
                        return null;
                    }

                    imageData = ReadImageData(dataset);

                    var serializer = new XmlSerializer(typeof(ImageData));
                    var outputStream = File.Create(outputFilename);
                    serializer.Serialize(outputStream, imageData);
                    outputStream.Close();
                }
            }

            return imageData;
        }

        public static List<string> Sort(string outputDirectory, int everyN, params string[] inputSliceFilenames)
        {
            var imageDataInfoList = new List<ImageDataInfo>();
            var serializer = new XmlSerializer(typeof(ImageData));
            foreach (var inputFilename in inputSliceFilenames)
            {
                using (var stream = File.OpenRead(inputFilename))
                {
                    var imageData = (ImageData)serializer.Deserialize(stream);
                    imageDataInfoList.Add(new ImageDataInfo
                    {
                        Filename = inputFilename, 
                        SliceLocation = imageData.SliceLocation
                    });
                }
            }

            Directory.CreateDirectory(outputDirectory);
            var outputSlices = new List<string>();
            imageDataInfoList.Sort((a, b) => (a.SliceLocation.CompareTo(b.SliceLocation)));
            for (var i = 0; i < imageDataInfoList.Count; i++)
            {
                var sortInfo = imageDataInfoList[i];
                var outputFilename = Path.Combine(outputDirectory, String.Format("{0:00000}-" + Path.GetFileName(sortInfo.Filename), i));
                File.Copy(sortInfo.Filename, outputFilename, true);
                outputSlices.Add(outputFilename);
            }

            return outputSlices.Where((t, i) => i % everyN == 0).ToList();
        }

        private unsafe static ImageData ReadImageData(IDictionary<uint, Element> dataset)
        {
            var imageData = new ImageData();

            var element = dataset[Dicom.ReverseDictionary["PixelData"]];
            imageData.PixelData = element.Value[0].Bytes;

            element = dataset[Dicom.ReverseDictionary["Rows"]];
            imageData.Rows = (int)element.Value[0].Long;

            element = dataset[Dicom.ReverseDictionary["Columns"]];
            imageData.Columns = (int)element.Value[0].Long;

            element = dataset[Dicom.ReverseDictionary["WindowWidth"]];
            imageData.WindowWidth = element.Value[0].Double;

            element = dataset[Dicom.ReverseDictionary["WindowCenter"]];
            imageData.WindowCenter = element.Value[0].Double;

            element = dataset[Dicom.ReverseDictionary["RescaleIntercept"]];
            imageData.RescaleIntercept = element.Value[0].Double;

            element = dataset[Dicom.ReverseDictionary["RescaleSlope"]];
            imageData.RescaleSlope = element.Value[0].Double;

            element = dataset[Dicom.ReverseDictionary["ImageOrientationPatient"]];
            imageData.ImageOrientationPatient = (from v in element.Value select v.Double).ToArray();

            element = dataset[Dicom.ReverseDictionary["ImagePositionPatient"]];
            imageData.ImagePositionPatient = (from v in element.Value select v.Double).ToArray();

            // Calculate SliceLocation.
            var x = new Vector { X = imageData.ImageOrientationPatient[0], Y = imageData.ImageOrientationPatient[1], Z = imageData.ImageOrientationPatient[2] };
            var y = new Vector { X = imageData.ImageOrientationPatient[3], Y = imageData.ImageOrientationPatient[4], Z = imageData.ImageOrientationPatient[5] };
            var z = new Vector();
            z.Cross(x, y);

            imageData.SliceLocation = imageData.ImagePositionPatient[0] * z.X + imageData.ImagePositionPatient[1] * z.Y + imageData.ImagePositionPatient[2] * z.Z;

            // Check pixel padding value - application is later.
            element = dataset[Dicom.ReverseDictionary["PixelRepresentation"]];
            var pixelRepresentation = element.Value[0].Long;

            var pixelPaddingValue = long.MinValue;
            var pixelPaddingValueId = Dicom.ReverseDictionary["PixelPaddingValue"];
            if (dataset.ContainsKey(pixelPaddingValueId))
            {
                element = dataset[pixelPaddingValueId];
                pixelPaddingValue = element.Value[0].Long;
                if (pixelRepresentation == 1)
                {
                    pixelPaddingValue -= ushort.MinValue;
                }
            }

            // Convert to unsigned short pixel data.
            if (pixelRepresentation == 1) // Need to convert to unsigned short.
            {
                fixed (byte* pixelData = imageData.PixelData)
                {
                    var shortPixelData = (short*)pixelData;
                    var ushortPixelData = (ushort*)pixelData;
                    var pixelCount = imageData.Rows * imageData.Columns;
                    for (var i = 0; i < pixelCount; i++)
                    {
                        ushortPixelData[i] = (ushort)(shortPixelData[i] - short.MinValue);
                    }
                }

                imageData.RescaleIntercept -= short.MinValue;
            }

            // Apply pixel padding value and record minimum and maximum intensities.
            fixed (byte* pixelData = imageData.PixelData)
            {
                var ushortPixelData = (ushort*)pixelData;
                var pixelCount = imageData.Rows * imageData.Columns;
                for (var i = 0; i < pixelCount; i++)
                {
                    if (ushortPixelData[i] == pixelPaddingValue)
                    {
                        ushortPixelData[i] = 0;
                    }

                    var value = ushortPixelData[i];
                    imageData.MinIntensity = Math.Min(imageData.MinIntensity, value);
                    imageData.MaxIntensity = Math.Max(imageData.MaxIntensity, value);
                }
            }

            // Calculate image physical dimensions in mm.
            element = dataset[Dicom.ReverseDictionary["PixelSpacing"]];
            var pixelSpacing = element;

            imageData.Width = imageData.Columns * pixelSpacing.Value[0].Double;
            imageData.Height = imageData.Rows * pixelSpacing.Value[0].Double;

            return imageData;
        }

        private static bool VerifyDataFormat(IDictionary<uint, Element> dataset)
        {
            if (dataset.Count <= 0)
            {
                return false;
            }

            var element = dataset[Dicom.ReverseDictionary["PhotometricInterpretation"]];
            var pi = element.Value[0].Text;
            if (pi != "MONOCHROME2")
            {
                Logger.Warn("Unable to convert DICOM other than MONOCHROME2: " + pi);
                return false;
            }

            return true;
        }

        public static List<string> CreateVolume(string outputDirectory, string outputName, params string[] sortedInputSliceFilenames)
        {
            var volumeData = new VolumeData();
            var imageSerializer = new XmlSerializer(typeof(ImageData));
            var volumeSerializer = new XmlSerializer(typeof (VolumeData));

            Directory.CreateDirectory(outputDirectory);

            var volumeRawFilename = Path.Combine(outputDirectory, outputName + ".raw");
            var volumeRawStream = File.Create(volumeRawFilename);
            var sliceCount = 0;

            foreach (var inputFilename in sortedInputSliceFilenames)
            {
                try
                {
                    using (var inputStream = File.OpenRead(inputFilename))
                    {

                        var imageData = (ImageData) imageSerializer.Deserialize(inputStream);
                        volumeRawStream.Write(imageData.PixelData, 0, imageData.PixelData.Length);
                        if (sliceCount == 0) // Use first slice as reference slice for volume.
                        {
                            volumeData.FirstSliceLocation = imageData.SliceLocation;
                            volumeData.Columns = imageData.Columns;
                            volumeData.Rows = imageData.Rows;
                            volumeData.Height = imageData.Height;
                            volumeData.Width = imageData.Width;
                            volumeData.ImageOrientationPatient = imageData.ImageOrientationPatient;
                            volumeData.ImagePositionPatient = imageData.ImagePositionPatient;
                            volumeData.RescaleIntercept = imageData.RescaleIntercept;
                            volumeData.RescaleSlope = imageData.RescaleSlope;
                            volumeData.WindowCenter = imageData.WindowCenter;
                            volumeData.WindowWidth = imageData.WindowWidth;
                        }

                        volumeData.MinIntensity = Math.Min(volumeData.MinIntensity, imageData.MinIntensity);
                        volumeData.MaxIntensity = Math.Max(volumeData.MaxIntensity, imageData.MaxIntensity);
                        volumeData.LastSliceLocation = imageData.SliceLocation;
                        sliceCount++;

                    }
                }
                catch (Exception err)
                {
                    Logger.Debug("Problem reading slice: " + inputFilename + ". " + err.Message);
                }
            }
            volumeRawStream.Close();

            volumeData.Depth = Math.Abs(volumeData.LastSliceLocation - volumeData.FirstSliceLocation);
            volumeData.Slices = sliceCount;

            var volumeXmlFilename = Path.Combine(outputDirectory, outputName + ".xml");
            var volumeXmlStream = File.Create(volumeXmlFilename);
            volumeSerializer.Serialize(volumeXmlStream, volumeData);
            volumeXmlStream.Close();

            return new List<string> { volumeXmlFilename, volumeRawFilename };
        }
    }
}
