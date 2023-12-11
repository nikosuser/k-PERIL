//https://www.linkedin.com/in/panagiotis-kalogeropoulos-3a2a95270/
// This section made by Panagiotis kalogeropoulos. 


// Using MaxRev-GDAL.
// Recommended on the official GDAL docs: https://gdal.org/api/csharp/index.html#getting-gdal-for-c
// TODO: Check https://github.com/MaxRev-Dev/gdal.netcore for details 
using DotSpatial.Data;
using MaxRev.Gdal.Core;
using OSGeo.GDAL;
using System;
using System.IO;

namespace GeoTiffHelpers
{

    public class GeoTiffHelpers
    {
        //Driver code
        public static void convertGeotiffToAsc(string geotiffFilePath, string outputPath)
        {
            // Register GDAL drivers
            // REQUIRED, DONT FORGET
            GdalBase.ConfigureAll();

            Band band = GetBand(geotiffFilePath);

            if (band != null)
            {
                float[] arr = GetRasterAsArray(band);

                OutputToFile(outputPath, band, arr);

                Console.WriteLine("GeoTIFF conversion to text completed.");
            }
            else
            {
                Console.WriteLine("Failed to open GeoTIFF file.");
            }
        }

        private static float[] GetRasterAsArray(Band band)
        {
            int width = band.XSize;
            int height = band.YSize;
            int size = width * height;

            float[] data = new float[size];
            var arr = band.ReadRaster(0, 0, width, height, data, width, height, 0, 0);
            if (arr != CPLErr.CE_None)
            {
                throw new NullException();
            }
            return data;
        }

        private static Band GetBand(string fileDir)
        {
            Dataset dataset = Gdal.Open(fileDir, Access.GA_ReadOnly);
            Band band = dataset.GetRasterBand(1);
            if (band == null)
            {
                throw new NullException();
            }
            return band;
        }

        private static void OutputToFile(string fileDir, Band band, float[] data)
        {
            using var writer = new StreamWriter(fileDir);

            int width = band.XSize;
            int height = band.YSize;
            int size = width * height;
            writer.WriteLine($"NCOLS {width}");
            writer.WriteLine($"NROWS {height}");
            writer.WriteLine($"XLLCORNER {"idfk"}");
            writer.WriteLine($"YLLCORNER {"idfk either"}");
            writer.WriteLine($"CELLSIZE {"not that either"}");
            writer.WriteLine($"NODATA_VALUE {"y'already know"}");

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    writer.Write(data[(y * width) + x]);
                    writer.Write(" ");
                }
                writer.WriteLine();
            }
        }
    }
}