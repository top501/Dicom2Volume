﻿// ****************************************************************************************
// Copyright (C) 2010, Jorn Skaarud Karlsen 
// All rights reserved. 
//
// Redistribution and use in source and binary forms, with or without modification, are 
// permitted provided that the following conditions are met: 
//
// * Redistributions of source code must retain the above copyright notice, this list of 
//   conditions and the following disclaimer. 
// * Redistributions in binary form must reproduce the above copyright notice, this list 
//   of conditions and the following disclaimer in the documentation and/or other 
//   materials provided with the distribution. 
// * Neither the name of Dicom2Volume nor the names of its contributors may be used to 
//   endorse or promote products derived from this software without specific prior 
//   written permission. 
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY 
// EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF 
// MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL 
// THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, 
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT 
// OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT 
// LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE 
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//****************************************************************************************

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Windows.Forms;
using System.IO;

namespace Dicom2Volume
{
    public class ExtDcmConv
    {
        public static List<string> Convert(string outputDirectory, params string[] inputFilenames)
        {
            var outputFilenames = new List<string>();
            Directory.CreateDirectory(outputDirectory);

            // Convert to a format that the dicom loader can handle easily.
            foreach (var inputFilename in inputFilenames)
            {
                var outputFilename = Path.Combine(outputDirectory, Path.GetFileName(inputFilename));
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo()
                    {
                        FileName = Config.DicomConverter.Replace("$(StartupPath)", Application.StartupPath),
                        Arguments = Config.DicomConverterArguments.Replace("$(InputFilename)", inputFilename).Replace("$(OutputFilename)", outputFilename),
                        UseShellExecute = false
                    }
                };

                if (!process.Start())
                {
                    throw new IOException("Unable to execute external converter!");
                }

                process.WaitForExit();
                outputFilenames.Add(outputFilename);
            }

            return outputFilenames;
        }
    }
}
