using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NetTopologySuite.Noding;
using System.Text;

namespace RoxCaseGen
{
    public class RunModels
    {
        int[,] safetyMatrix;

        public static void runCommand(string[] commands, string exeLocation, string commandJoiner)
        {
            Process process = new Process();
            Console.WriteLine(String.Join(commandJoiner, commands));
            process.StartInfo = new ProcessStartInfo(exeLocation, String.Join("; ", commands))
            {
                UseShellExecute = true,
                CreateNoWindow = false,
                //RedirectStandardOutput = true
            };
            process.Start();
            process.WaitForExit();

            /*
            string script = string.Join(" ; ", commands);

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments =
                        $"-NoExit -Command \"Start-Process -FilePath '{exeLocation}' -ArgumentList '{script}' -Wait\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = false
                };

                using (Process process = Process.Start(psi))
                {
                    process.WaitForExit();

                    // Capture the output and errors
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();

                    // Print results
                    Console.WriteLine("Output:");
                    Console.WriteLine(output);

                    if (!string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine("Errors:");
                        Console.WriteLine(error);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
            */
        }

        public static void CopySpecificFiles(string sourceFolder, string destinationFolder, string[] fileExtensions)
        {
            // Ensure the source directory exists
            if (!Directory.Exists(sourceFolder))
            {
                throw new DirectoryNotFoundException($"Source folder not found: {sourceFolder}");
            }

            // Ensure the destination directory exists; create it if it doesn't
            if (!Directory.Exists(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
            }

            // Get all files in the source folder
            string[] files = Directory.GetFiles(sourceFolder);

            foreach (string file in files)
            {
                // Get the file extension
                string fileExtension = Path.GetExtension(file);

                // Check if the file extension is in the allowed list
                if (fileExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase))
                {
                    // Get the file name
                    string fileName = Path.GetFileName(file);

                    // Construct the destination file path
                    string destinationFile = Path.Combine(destinationFolder, fileName);

                    // Copy the file to the destination folder
                    File.Copy(file, destinationFile, overwrite: true);
                }
            }

            Console.WriteLine(
                $"Files with extensions {string.Join(", ", fileExtensions)} copied from {sourceFolder} to {destinationFolder}");
        }

        static void checkFileExists(string file)
        {
            try
            {
                if (!File.Exists(file))
                {
                    throw new FileNotFoundException();
                }
            }
            catch (FileNotFoundException e)
            {
                Console.WriteLine($"File {file} was not found during setup.");
            }
        }

        static void initiateModels()
        {
            //delete outputs from previous iterations



            //delete existing jobs in wise
            System.IO.DirectoryInfo dir = new System.IO.DirectoryInfo(@"C:\Users\");

            //start manager and builder

        }

        public static void checkInputsExist(string path)
        {
            string[] necessaryFiles =
            [
                "/VARS.txt",
                "/FUELTEMPLATE.asc",
                "/Farsite/Input/elevation.asc",
                "/Farsite/Input/slope.asc",
                "/Farsite/Input/aspect.asc",
                "/Farsite/Input/fuel.asc",
                "/Farsite/Input/canopycover.asc",
                "/Farsite/Input/canopybulkdensity.asc",
                "/Farsite/Input/canopybaseheight.asc",
                "/Farsite/Input/canopyheight.asc",
                "/Farsite/Input/landscape.lcp",
                "/Farsite/ROX.txt",
                "/WISE/Input/fuel.asc",
                "/WISE/Input/fuel.prj",
                "/WISE/Input/elevation.asc",
                "/WISE/Input/elevation.prj",
                "/WISE/Input/fbp_lookup_table.lut",
                "/FDS LS1/Input/peril.qgz",
                "/FDS LS1/Input/fuel13.tif",
                "/FDS LS1/Input/dem_big.tif",
                "/FDS LS1/Input/Landfire.gov_F13.csv",
                "/ELMFIRE/Input/adj.tif",
                "/ELMFIRE/Input/asp.tif",
                "/ELMFIRE/Input/cbd.tif",
                "/ELMFIRE/Input/cbh.tif",
                "/ELMFIRE/Input/cc.tif",
                "/ELMFIRE/Input/ch.tif",
                "/ELMFIRE/Input/dem.tif",
                "/ELMFIRE/Input/fbfm40.tif",
                "/ELMFIRE/Input/slp.tif",
                "/ELMFIRE/Input/phi.tif",
                "/ELMFIRE/run.sh",
                "/ELMFIRE/elmfire.data.in",
                "/EPD/fuel.asc"
            ];

            foreach (string file in necessaryFiles)
            {
                checkFileExists(path + "/Input/" + file);
                File.Copy(path + "Input" + file, path + file, true);
            }
        }

        public static void setupFarsiteIteration(string path, ModelSetup please)
        {
            int[] coordinates = please.SelectIgnitionPoint(path, false); //choose ignition coordinates
            please.randomizeValues(); //randomise the weather and wind values
            please.saveUsedData(path); //dump the selected variables in log.txt
            please.createTempAndHumidProfile(); //extrapolate diurnal temperature and humidity profiles

            please.createAndWriteFileFARSITE(path); //create the FARSITE input file

            createShapefile.createAndWriteShapefile(coordinates[0], coordinates[1],
                path); //create the shapefile for the ignition point
        }

        public static void convertToSpecificModel(string model, string path, ModelSetup please)
        {
            switch (model)
            {
                case "Farsite":
                    break;
                case "WISE":
                    string[,] wise_weather = new string[please.totalRAWSdays * 24 + 1, 7];
                    wise_weather[0, 0] = "HOURLY";
                    wise_weather[0, 1] = "HOUR";
                    wise_weather[0, 2] = "TEMP";
                    wise_weather[0, 3] = "RH";
                    wise_weather[0, 4] = "WD";
                    wise_weather[0, 5] = "WS";
                    wise_weather[0, 6] = "PRECIP";
                    for (int i = 0;
                         i < please.totalRAWSdays;
                         i++) //Output all the weather data in FARSITE flavor. Precipitation is defaulted to 20%
                    {
                        for (int j = 0; j < 24; j++)
                        {
                            wise_weather[i * 24 + j + 1, 0] =
                                $"{(i + 1).ToString("D" + 2)}/{Int16.Parse(please.month).ToString("D" + 2)}/{please.year}";
                            wise_weather[i * 24 + j + 1, 1] = j.ToString("D" + 2);
                            wise_weather[i * 24 + j + 1, 2] = please.tempProfile[j].ToString();
                            wise_weather[i * 24 + j + 1, 3] = please.humidProfile[j].ToString();
                            wise_weather[i * 24 + j + 1, 4] = please.windDir.ToString();
                            wise_weather[i * 24 + j + 1, 5] = please.windMag.ToString();
                            wise_weather[i * 24 + j + 1, 6] = 0.ToString();
                        }
                    }

                    // Get the number of rows and columns
                    using (StreamWriter writer = new StreamWriter(path + "WISE/Input/weather.txt"))
                    {
                        for (int i = 0; i < wise_weather.GetLength(0); i++)
                        {
                            string[] row = new string[wise_weather.GetLength(1)];
                            for (int j = 0; j < wise_weather.GetLength(1); j++)
                            {
                                row[j] = wise_weather[i, j];
                            }

                            writer.Write(string.Join(",", row) + "\n");
                        }
                    }

                    //how get elevation of weather station?
                    double[] ignitionCoords = ModelSetup.convertCoords((double)please.xIgnition_prj,
                        (double)please.yIgnition_prj, 35, true);
                    Console.WriteLine($"{ignitionCoords[0]},{ignitionCoords[1]}");
                    Dictionary<string, string> simulationSetup = new Dictionary<string, string>();

                    simulationSetup.Add("Input Directory", path + "WISE/Input/");
                    simulationSetup.Add("FBP Fuel Map File Name", "fuel.asc");
                    simulationSetup.Add("FBP Fuel Map Lookup Table File Name", "fbp_lookup_table.lut");
                    simulationSetup.Add("Elevation File Name", "elevation.asc");
                    simulationSetup.Add("Elevation Projection File Name", "elevation.prj");
                    simulationSetup.Add("Weather File Name", "weather.txt");
                    simulationSetup.Add($"Ignition Time",
                        $"{please.year}-{Int16.Parse(please.month).ToString("D" + 2)}-{(please.totalRAWSdays - please.burnDuration / 24).ToString("D" + 2)}T{(23 - please.burnDuration % 24).ToString("D" + 2)}:00:00-00:00");
                    simulationSetup.Add("Ignition Coords", $"{ignitionCoords[1]} ,{ignitionCoords[0]}");
                    simulationSetup.Add("Simulation End Time",
                        $"{please.year}-{Int16.Parse(please.month).ToString("D" + 2)}-{please.totalRAWSdays.ToString("D" + 2)}T23:00:00-00:00");
                    simulationSetup.Add("Weather Station Height",
                        $"{ModelSetup.getElevation((int)please.xIgnition_raster, (int)please.yIgnition_raster, path + "WISE/Input/elevation.asc")}"); //<------------
                    simulationSetup.Add("Weather Station Coords",
                        $"{ignitionCoords[1]} ,{ignitionCoords[0]}"); //<------------
                    simulationSetup.Add("Weather Station Start Date",
                        $"{please.year}-{Int16.Parse(please.month).ToString("D" + 2)}-01");
                    simulationSetup.Add("Weather Station End Date",
                        $"{please.year}-{Int16.Parse(please.month).ToString("D" + 2)}-{please.totalRAWSdays.ToString("D" + 2)}");

                    using (StreamWriter file = new StreamWriter(path + @"WISE/wise_in.txt"))
                    {
                        foreach (var entry in simulationSetup)
                        {
                            file.WriteLine("{0}", entry.Value);
                        }
                    }

                    break;
                case "ELMFIRE":
                    string[,] output = new string[please.burnDuration + 1, 7];
                    output[0, 0] = "ws";
                    output[0, 1] = "wd";
                    output[0, 2] = "m1";
                    output[0, 3] = "m10";
                    output[0, 4] = "m100";
                    output[0, 5] = "lh";
                    output[0, 6] = "lw";
                    for (int i = 1; i < please.burnDuration; i++)
                    {
                        output[i, 0] = please.windMag.ToString();
                        output[i, 1] = please.windDir.ToString();
                        output[i, 2] = please.fuelMoisture[0].ToString();
                        output[i, 3] = please.fuelMoisture[1].ToString();
                        output[i, 4] = please.fuelMoisture[2].ToString();
                        output[i, 5] = please.fuelMoisture[3].ToString();
                        output[i, 6] = please.fuelMoisture[4].ToString();
                    }

                    // Get the number of rows and columns
                    int rows = output.GetLength(0);
                    int columns = output.GetLength(1);
                    using (StreamWriter writer = new StreamWriter(path + "ELMFIRE/weather.csv"))
                    {
                        for (int i = 0; i < rows; i++)
                        {
                            string[] row = new string[columns];
                            for (int j = 0; j < columns; j++)
                            {
                                row[j] = output[i, j];
                            }

                            writer.Write(string.Join(",", row) + "\n");
                        }
                    }

                    string text = File.ReadAllText(path + @"ELMFIRE/run.sh");
                    text = text.Replace("SIMULATION_TSTOP=32400.0", $"SIMULATION_TSTOP={please.burnDuration * 3600}");
                    text = text.Replace("WX_READINGS=10", $"WX_READINGS={please.burnDuration}");
                    text = text.Replace("XIGN=232043.4", $"XIGN={please.xIgnition_prj}");
                    text = text.Replace("YIGN=4215113.9", $"YIGN={please.yIgnition_prj}");
                    File.WriteAllText(path + @"ELMFIRE/run.sh", text);

                    ModelSetup.Copy(path + "ELMFIRE/",
                        @"\\wsl.localhost\\Ubuntu-22.04\\home\\nikosuser\\ELMFIRE\\elmfire\\tutorials\\kPERIL\");
                    break;
                case "EPD":
                case "LSTM":

                    List<string> wxsOutput = new List<string>();
                    List<string> fmsOutput = new List<string>();
                    for (int i = 1; i < 256; i++)
                    {
                        fmsOutput.Add((i - 1).ToString() +
                                      $" {please.fuelMoisture[0]} {please.fuelMoisture[1]} {please.fuelMoisture[2]} {please.fuelMoisture[3]} {please.fuelMoisture[4]}");
                    }

                    wxsOutput.Add("RAWS_ELEVATION: 10"); //random value added for now. Maybe it is too high?
                    wxsOutput.Add("RAWS_UNITS: Metric"); //All values declared metric
                    wxsOutput.Add(
                        $"RAWS: {(please.totalRAWSdays * 24).ToString()}"); //Calculate and declare how many weather points will follow
                    for (int i = 0;
                         i < please.totalRAWSdays;
                         i++) //Output all the weather data in FARSITE flavor. Precipitation is defaulted to 20%
                    {
                        for (int j = 0; j < 24; j++)
                        {
                            wxsOutput.Add(
                                $"{please.year} {please.month} {i + 1} {j * 100} {(int)please.tempProfile[j]} {(int)please.humidProfile[j]} 0.00 {(int)please.windMag} {please.windDir} 0");
                        }
                    }

                    File.WriteAllLines(path + "/" + model + "/Input/moisture.fms", fmsOutput.ToArray());
                    File.WriteAllLines(path + "/" + model + "/Input/weather.wxs", wxsOutput.ToArray());

                    string sourceFolder = path + "/Farsite/Input/";
                    string destinationFolder = path + "/" + model + "/Input/";

                    string[] fileExtensions = { ".asc", ".fms", ".wxs", ".shp" };

                    try
                    {
                        CopySpecificFiles(sourceFolder, destinationFolder, fileExtensions);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: {ex.Message}");
                    }

                    break;
                case "FDS LS1":
                case "FDS LS4":
                    List<string> fdsWeather = new List<string>();
                    fdsWeather.Add("times,speed,direction");
                    for (int i = 0; i < please.burnDuration; i++)
                    {
                        fdsWeather.Add($"{i * 3600},{please.windMag / 3.6},{please.windDir}");
                    }

                    File.WriteAllLines(path + "/" + model + "/Input/weather.csv", fdsWeather.ToArray());

                    string addCRStoPointCommand =
                        @$" run native:reprojectlayer --distance_units=meters --area_units=m2 --ellipsoid=EPSG:7043 --INPUT='D:/OneDrive - Imperial College London/Imperial/PhD/k2PERIL/Farsite/Input/ROX.shx|layername=ROX' --TARGET_CRS='EPSG:32235' --CONVERT_CURVED_GEOMETRIES=false --OPERATION= --OUTPUT='D:/OneDrive - Imperial College London/Imperial/PhD/k2PERIL/{model}/Input/ignition.shp'";
                    string qgisProcessorPromptPath = @"C:\Program Files\QGIS 3.34.11\bin/";
                    string[] commandsPoint = new string[]
                    {
                        $"cd '{qgisProcessorPromptPath}'",
                        $@".\qgis_process-qgis-ltr.bat {addCRStoPointCommand}"
                    };
                    runCommand(commandsPoint, @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",";");

                    string inputPath = $"{path}/{model}/Input/";
                    string qgisCommand = 
                        @$"--PROJECT_PATH='{path}/FDS LS1/Input/peril.qgz' " +
                        @$"--distance_units=meters --area_units=m2 " +
                        @$"--ellipsoid=EPSG:7043 " +
                        @$"--chid='mati' " +
                        @$"--fds_path='{inputPath}' " +
                        @$"--extent_layer='{path}\Input\FDS LS1\Input\fuelExtent.shp' " +
                        @$"--pixel_size=90 " +
                        @$"--dem_layer='{path}/FDS LS1/Input/dem_big.tif' " +
                        @$"--landuse_layer='{path}/FDS LS1/Input/fuel13.tif' " +
                        @$"--landuse_type_filepath='{path}\Input\FDS LS1\Input\Landfire.gov_F13.csv' " +
                        @$"--fire_layer='{path}/FDS LS1/Input/ignition.shp' " +
                        @$"--wind_filepath='{path}/FDS LS1/Input/weather.csv' " +
                        @$"--tex_pixel_size={please.cellsize} " +
                        @$"--tex_layer='{path}/FDS LS1/Input/fuel13.tif' " +
                        @$"--nmesh=35 " +
                        @$"--cell_size=90 " +
                        @$"--t_begin=0 " +
                        @$"--t_end={please.burnDuration*3600} " +
                        @$"--text_filepath= " +
                        $@"--UtmGrid=TEMPORARY_OUTPUT " + 
                        @$"--export_obst=true";

                    string[] commands = new string[]
                    {
                        "C:",
                        $"cd '{qgisProcessorPromptPath}'",
                        $@".\qgis_process-qgis-ltr.bat run 'NIST FDS:Export FDS case' {qgisCommand}"
                    };
                    Console.WriteLine(commands);
                    int lsmode = (model == "FDS LS1") ? 1 : 4;

                    runCommand(commands, @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",";");
                    string filePath = $"{path}/{model}/Input/mati.fds"; // Replace with the actual path to your file

                    string fdstext = File.ReadAllText(filePath);
                    fdstext = fdstext.Replace("_REAC ID='Wood' SOOT_YIELD=0.005 O=2.5 C=3.4 H=6.2",
                        $"&REAC ID='Wood' SOOT_YIELD=0.005 O=2.5 C=3.4 H=6.2");
                    fdstext = fdstext.Replace("      LEVEL_SET_MODE=1 ", $"      LEVEL_SET_MODE={lsmode} ");
                    fdstext = fdstext.Replace("&WIND SPEED=1., RAMP_SPEED_T='ws', RAMP_DIRECTION_T='wd' /", $"&WIND SPEED=1., RAMP_SPEED='ws', RAMP_DIRECTION='wd' /");
                    fdstext = fdstext.Replace("&TIME T_END=0. /", $"&TIME T_END={please.burnDuration * 3600} /");
                    for (int i = 0; i < 11; i++)
                    {
                        fdstext = fdstext.Replace($"VEG_LSET_FUEL_INDEX={i + 1} /",
                            $"VEG_LSET_FUEL_INDEX={i + 1}, VEG_LSET_M1={(please.fuelMoisture[0] / 100).ToString("F")}, VEG_LSET_M10={(please.fuelMoisture[1] / 100).ToString("F")}, VEG_LSET_M100={(please.fuelMoisture[2] / 100).ToString("F")}, VEG_LSET_MLW={(please.fuelMoisture[3] / 100).ToString("F")}, VEG_LSET_MLH={(please.fuelMoisture[4] / 100).ToString("F")} /");
                    }

                    File.WriteAllText(filePath, fdstext);
                    //yes I am doing double work with this, but I cannot be bothered to change the above.
                    string[] lines = File.ReadAllLines(filePath);

                    int targetIndex = Array.FindIndex(lines,
                        line => line.Contains("&SLCF PBY=0.00 QUANTITY='TEMPERATURE' VECTOR=T /"));

                    if (targetIndex != -1)
                    {
                        var updatedLines = lines.Take(targetIndex + 1)
                            .Concat(new[] { "&SLCF AGL_SLICE=5. QUANTITY='TIME OF ARRIVAL' /" })
                            .Concat(lines.Skip(targetIndex + 1))
                            .ToArray();

                        // Write the updated lines back to the file
                        File.WriteAllLines(filePath, updatedLines);
                    }
                    else
                    {
                        Console.WriteLine("Target line not found!");
                    }

                    targetIndex = Array.FindIndex(lines,
                        line => line.Contains(" 4: Wind and fire fully-coupled."));

                    if (targetIndex != -1)
                    {
                        var updatedLines = lines.Take(targetIndex + 1)
                            .Concat(new[] { "&RADI RADIATION=F/" })
                            .Concat(lines.Skip(targetIndex + 1))
                            .ToArray();

                        // Write the updated lines back to the file
                        File.WriteAllLines(filePath, updatedLines);
                    }
                    else
                    {
                        Console.WriteLine("Target line not found!");
                    }
                    break;
            }
        }

        public static void RunModel(string model, string path, ModelSetup modelSetup, int simNo)
        {
            Console.WriteLine($"Running Model: {model}");
            switch (model)
            {
                case "Farsite":
                    //
                    runCommand([$"cd '{path}Farsite'", $"./setenv.bat", "./bin/testFARSITE.exe ROX.txt"],
                        @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",";");
                    break;
                case "WISE":
                    
                    Console.WriteLine("Starting WISE Manager");
                    ProcessStartInfo startInfoManager = new ProcessStartInfo(@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe", @"cd C:\WISE_Manager-0.6.beta.5; java -jar WISE_Manager_Ui.jar")
                    {
                        UseShellExecute = false, // Allows redirection
                        RedirectStandardOutput = true, // Manage output
                        RedirectStandardError = true,
                        CreateNoWindow = true // Prevents the window from appearing
                    };
                    Process procBuilderManager = Process.Start(startInfoManager);
                    
                    Thread.Sleep(4000);
/*
                    //Process builder = startBuilder();
                    Console.WriteLine("Starting WISE Builder");
                    ProcessStartInfo startInfo = new ProcessStartInfo(@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe", @"cd C:\WISE_Builder-1.0.6-beta.5; java -jar WISE_Builder.jar -s -j C:\jobs")
                    {
                        UseShellExecute = false, // Allows redirection
                        RedirectStandardOutput = true, // Manage output
                        RedirectStandardError = true,
                        CreateNoWindow = true // Prevents the window from appearing
                    };
                    Process procBuilder = Process.Start(startInfo);
                    
                    Thread.Sleep(8000);
                    */
                    string[] WISEresults =
                        Directory.GetDirectories(@"C:\\jobs", "job_*", SearchOption.TopDirectoryOnly);
                    if (WISEresults.Length > 0)
                    {
                        for (int i = 0; i < WISEresults.Length; i++)
                        {
                            Directory.Delete(WISEresults[i], true);
                        }
                    }

                    runCommand(
                    [
                        $@"cd '{Environment.GetEnvironmentVariable("WISEPREPROCPATH")}'",
                        $@"& './WISE Preprocessor.exe' '{path}\WISE\wise_in.txt'"
                    ], @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",";");
                    
                    WISEresults =
                        Directory.GetDirectories(@"C:\\jobs", "job_*", SearchOption.TopDirectoryOnly);
                    while (WISEresults.Length == 0)
                    {
                        WISEresults =
                            Directory.GetDirectories(@"C:\\jobs", "job_*", SearchOption.TopDirectoryOnly);
                        Thread.Sleep(5000);
                    }
                    runCommand(["cd 'C:/Program Files/WISE/'",@$".\wise.exe -t '{WISEresults[0]}\job.fgmj'"],@"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",";");
                    
                    Console.WriteLine("Killing WISE Builder");
                    //procBuilder.Kill();

                    break;
                case "ELMFIRE":
                    runCommand(
                    [
                        @"cd \\wsl.localhost\Ubuntu-22.04\home\nikosuser\ELMFIRE\elmfire\tutorials\kPERIL",
                        "bash ./run.sh"
                    ], @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",";");
                    break;
                case "EPD":
                case "LSTM":
                    /*
                    string anacondaPromptPath =
                        @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";
                    string[] commands = new string[]
                    {
                        @"C:\\Users\\nikos\\miniconda3\\Scripts\\activate.bat",
                        "conda activate newEnv",
                        $@"python D:\\GoogleModel\\wildfire_conv_ltsm\\preprocessor.py california {model} '{path}/EPD/Input' {modelSetup.burnDuration} {modelSetup.xIgnition_raster} {modelSetup.yIgnition_raster} 2022 09 {modelSetup.totalRAWSdays - modelSetup.burnDuration/24} {2300 - modelSetup.burnDuration%24}",
                        "pause",
                        "exit"
                    };
                    runCommand(commands, anacondaPromptPath);
                    */
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            RedirectStandardInput = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            WorkingDirectory = path + @"\" + model,
                        }
                    };
                    process.Start();
                    // Pass multiple commands to cmd.exe
                    using (var sw = process.StandardInput)
                    {
                        if (sw.BaseStream.CanWrite)
                        {
                            sw.WriteLine(@"C:/Users/nikos/miniconda3/Scripts/activate.bat");
                            sw.WriteLine("conda activate newEnv");
                            sw.WriteLine(
                                $"python D:/GoogleModel/wildfire_conv_ltsm/preprocessor.py california {model} \"D:/OneDrive - Imperial College London/Imperial/PhD/k2PERIL/{model}/Input\" {modelSetup.burnDuration} {modelSetup.xIgnition_raster} {modelSetup.yIgnition_raster} {modelSetup.year} {modelSetup.month} {modelSetup.totalRAWSdays - (int)(modelSetup.burnDuration / 24)} {23 - modelSetup.burnDuration % 24}00");
                            sw.WriteLine("exit");
                        }
                    }

                    while (!process.StandardOutput.EndOfStream)
                    {
                        var line = process.StandardOutput.ReadLine();
                        Console.WriteLine(line);
                    }

                    break;
                case "FDS LS1":
                case "FDS LS4":
                    //runCommand([$"\"C:/Program Files/firemodels/FDS6/bin/fdsinit.bat\"","D:",$"cd \"{path}{model}/Input/\"","mpiexec -n 12 fds mati.fds"],@"C:\WINDOWS\system32\cmd.exe"," && ");
                    
                    //running it now is way too slow (10M cells). Save it in a folder and yeet it to the hpc later. 
                    string copyFolder = path + model + "/Iter" + simNo.ToString("000");
                    if (Directory.Exists(copyFolder))
                    {
                        Directory.Delete(copyFolder, true);
                    }
                    Directory.CreateDirectory(copyFolder);
                    File.Copy($@"{path}{model}/Input/mati.fds", copyFolder+"/mati.fds", true);
                    if (File.Exists($"{path}{model}/Input/mati_tex.png"))
                    {
                        File.Copy($"{path}{model}/Input/mati_tex.png", copyFolder + "/mati_tex.png", true);
                    }

                    // Path to the text file
                    string fdsfilePath = $@"{path}{model}/Input/mati.fds";

                    // Target string pattern to find
                    string targetPattern = @"(\d+)\s·\s(\d+)\smeshes\s";

                    int coreNo = 0;

                    try
                    {
                        // Read all lines of the file
                        string[] lines = File.ReadAllLines(fdsfilePath);

                        foreach (string line in lines)
                        {
                            if (line.Contains("meshes"))
                            {
                                // Match the pattern to extract the two numbers
                                Match match = Regex.Match(line, targetPattern);

                                if (match.Success)
                                {
                                    // Extract the first two numbers
                                    int num1 = int.Parse(match.Groups[1].Value);
                                    int num2 = int.Parse(match.Groups[2].Value);

                                    // Calculate the multiplication
                                    coreNo = num1 * num2;
                                }
                            }
                        }

                        Console.WriteLine("Target line not found.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: {ex.Message}");
                    }
                    
                    
                    string scriptContent = @$"#!/bin/bash
#PBS -lwalltime=00:30:00
#PBS -lselect=1:ncpus={coreNo}:mem=32gb
#PBS -o output.txt
#PBS -e error.txt

cd $PBS_O_WORKDIR

module load mpi
module load fds

mpirun fds $HOME/peril/Iter{simNo.ToString("000")}/mati.fds";

                    // File path
                    string filePath = $"{copyFolder}/mati{simNo.ToString("000")}.sh";

                    // Write the file with LF line endings
                    File.WriteAllText(filePath, scriptContent.Replace("\r\n", "\n"));

                    scriptContent = $"qsub mati{simNo.ToString("000")}.sh";
                    filePath = $"{copyFolder}/run.sh";
                    File.WriteAllText(filePath, scriptContent.Replace("\r\n", "\n"));
                    break;
            }
        }

        public static float[,] retrieveResult(string model, string path, string outputKind, ModelSetup please)
        {
            float[,] output = new float[please.fuelMap.GetLength(0), please.fuelMap.GetLength(1)];
            string file = "";
            switch (model)
            {
                case "Farsite":
                    if (outputKind == "ROS")
                    {
                        file = path + @"Farsite/Median_Outputs/_SpreadRate.asc";
                    }
                    else if (outputKind == "Azimuth")
                    {
                        file = path + @"Farsite/Median_Outputs/_SpreadDirection.asc";
                    }

                    output = ModelSetup.readASC_float(file);
                    break;
                case "WISE":
                    if (outputKind == "ROS")
                    {
                        file = "ROS";
                    }
                    else if (outputKind == "Azimuth")
                    {
                        file = "RAZ";
                    }

                    Thread.Sleep(2000);
                    // Define the base directory and target subfolder
                    string baseDirectory = @"C:\jobs";
                    string targetSubfolder = @"Outputs\scen0";
                    string fileName = file + ".tif";
                    string destinationPath =
                        @$"{path}\WISE\Output\" + file +
                        ".tif";

                    var jobDirectories =
                        Directory.GetDirectories(baseDirectory, "job_*", SearchOption.TopDirectoryOnly);

                    foreach (var jobDir in jobDirectories)
                    {
                        // Construct the full path to the expected file
                        string filePath = Path.Combine(jobDir, targetSubfolder, fileName);

                        while (!File.Exists(filePath))
                        {
                        }

                        // File found, copy it to the destination
                        Thread.Sleep(2000);
                        File.Copy(filePath, destinationPath, true); // true to overwrite if it already exists
                        if (!File.Exists(path+"WISE/Output/AT.tif"))
                        {
                            File.Copy(jobDir + "\\" + targetSubfolder + "\\" + "AT.tif", path+"WISE/Output/AT.tif", true);
                        }
                    }
                    output = ModelSetup.readTiff(destinationPath);
                    break;
                case "ELMFIRE":
                    string sourceFolder =
                        @"\\wsl.localhost\Ubuntu-22.04\home\nikosuser\ELMFIRE\elmfire\tutorials\kPERIL\outputs/";
                    string destinationFolder =
                        @$"{path}\ELMFIRE\Median_output/";

                    string ELMoutputname = outputKind == "ROS" ? "vs" : "time_of_arrival";

                    Regex regex = new Regex(@$"{ELMoutputname}_\d+_\d+\.tif");
                    bool found = false;
                    while (!found)
                    {
                        if (Directory.Exists(sourceFolder))
                        {
                            var files = Directory.GetFiles(sourceFolder);
                            foreach (var ELMfile in files)
                            {
                                if (regex.IsMatch(Path.GetFileName(ELMfile)))
                                {
                                    Thread.Sleep(2000);
                                    string destinationFile = destinationFolder + ELMoutputname + ".tif";
                                    Directory.CreateDirectory(destinationFolder); // Ensure destination exists
                                    File.Copy(ELMfile, destinationFile, overwrite: true);
                                    found = true;
                                }
                            }

                            Thread.Sleep(1000);
                        }
                    }

                    float[,] output_temp = ModelSetup.readTiff(Path.Combine(destinationFolder, ELMoutputname + ".tif"));

                    if (outputKind == "Azimuth")
                    {
                        Console.WriteLine($"ELMFIRE: Converting Arrival Time to Azimuth ... ");
                        for (int i = 0; i < output.GetLength(0); i++)
                        {
                            for (int j = 0; j < output.GetLength(1); j++)
                            {
                                if (i == 0)
                                {
                                    output[i, j] = 0;
                                }
                                else if (i == output.GetLength(0) - 1)
                                {
                                    output[i, j] = 0;
                                }
                                else if (j == 0)
                                {
                                    output[i, j] = 0;
                                }
                                else if (j == output.GetLength(1) - 1)
                                {
                                    output[i, j] = 0;
                                }
                                else
                                {
                                    List<Vector> vectors = new List<Vector>
                                    {
                                        Vector.FromPolar(output_temp[i + 1, j], 0),
                                        Vector.FromPolar(output_temp[i + 1, j + 1], 45),
                                        Vector.FromPolar(output_temp[i, j + 1], 90),
                                        Vector.FromPolar(output_temp[i - 1, j + 1], 135),
                                        Vector.FromPolar(output_temp[i - 1, j], 180),
                                        Vector.FromPolar(output_temp[i - 1, j - 1], 225),
                                        Vector.FromPolar(output_temp[i, j - 1], 270),
                                        Vector.FromPolar(output_temp[i + 1, j - 1], 315),
                                    };
                                    Vector resultantVector = Vector.Add(vectors);
                                    output[i, j] = (float)resultantVector.Direction;
                                }
                            }
                        }
                    }
                    else
                    {
                        output = output_temp;
                    }

                    break;
                case "EPD":
                case "LSTM":
                    string googlePath = @$"{path}\{model}\Input\";

                    while (!File.Exists(googlePath + model + "_AT_OS.asc"))
                    {
                        Thread.Sleep(500);
                    }

                    float[,] output_EPD = ModelSetup.readASC_float(googlePath + model + "_AT_OS.asc");

                    Console.WriteLine($"Google: Converting Arrival Time to ROS ... ");
                    for (int i = 0; i < output.GetLength(0); i++)
                    {
                        for (int j = 0; j < output.GetLength(1); j++)
                        {
                            if ((int)output_EPD[i,j] == -9999)
                            {
                                output[i, j] = 0;
                            }
                            else if (i == 0)
                            {
                                output[i, j] = 0;
                            }
                            else if (i == output.GetLength(0) - 1)
                            {
                                output[i, j] = 0;
                            }
                            else if (j == 0)
                            {
                                output[i, j] = 0;
                            }
                            else if (j == output.GetLength(1) - 1)
                            {
                                output[i, j] = 0;
                            }
                            else
                            {
                                List<Vector> vectors = new List<Vector>
                                {
                                    Vector.FromPolar(output_EPD[i + 1, j], 0),
                                    Vector.FromPolar(output_EPD[i + 1, j + 1], 45),
                                    Vector.FromPolar(output_EPD[i, j + 1], 90),
                                    Vector.FromPolar(output_EPD[i - 1, j + 1], 135),
                                    Vector.FromPolar(output_EPD[i - 1, j], 180),
                                    Vector.FromPolar(output_EPD[i - 1, j - 1], 225),
                                    Vector.FromPolar(output_EPD[i, j - 1], 270),
                                    Vector.FromPolar(output_EPD[i + 1, j - 1], 315),
                                };
                                Vector resultantVector = Vector.Add(vectors);
                                output[i, j] = (outputKind == "ROS")
                                    ? (float)(please.cellsize / resultantVector.Magnitude)
                                    : (float)resultantVector.Direction + 180;
                            }
                        }
                    }
                    break;
                case "FDS LS1":
                case "FDS LS4":
                    /*
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            RedirectStandardInput = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            WorkingDirectory = path + @"\" + model,
                        }
                    };
                    process.Start();
                    // Pass multiple commands to cmd.exe
                    using (var sw = process.StandardInput)
                    {
                        if (sw.BaseStream.CanWrite)
                        {
                            // Vital to activate Anaconda
                            sw.WriteLine(@"C:\\Users\\nikos\\miniconda3\\Scripts\\activate.bat");
                            // Activate your environment
                            sw.WriteLine("conda activate newEnv");
                            // Any other commands you want to run
                            sw.WriteLine(
                                $@"python 'D:\\OneDrive - Imperial College London\\Imperial\\PhD\\FDS2GIS\\findArrivalTime.py' '{path}/{model}/Input/mati.fds/' mati {please.cellsize} {path}/Input/Farsite/Input/elevation");
                            sw.WriteLine("exit");
                        }
                    }

                    while (!process.StandardOutput.EndOfStream)
                    {
                        var line = process.StandardOutput.ReadLine();
                        Console.WriteLine(line);
                    }
                    */
                    break;
            }
            return output;
        }

        public static Process startBuilder()
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    WorkingDirectory = @"C:\WISE_Builder-1.0.6-beta.5",
                }
            };
            process.Start();
            // Pass multiple commands to cmd.exe
            using (var sw = process.StandardInput)
            {
                if (sw.BaseStream.CanWrite)
                {
                    sw.WriteLine(@"cd C:\WISE_Builder-1.0.6-beta.5; java -jar WISE_Builder.jar -s -j C:\jobs");
                }
            }
            return process;
        }
        public static void logArrivalTimes(string path, int simNo)
        {
            string[] necessaryFiles =
            [
                "/Farsite/Median_Outputs/_ArrivalTime.asc",
                "/Farsite/Median_Outputs/_ArrivalTime.prj",
                "/WISE/Output/AT.tif",
                "/ELMFIRE/Median_output/time_of_arrival.tif",
                "/EPD/Input/EPD_AT_OS.asc",
                "/LSTM/Input/LSTM_AT_OS.asc"
            ];

            foreach (string file in necessaryFiles)
            {
                if (File.Exists(path + "/" + file))
                    File.Copy(path + "/" + file, path + "Log/" + simNo.ToString("000_") + Path.GetFileName(file), true);
            }
        }
    }
}