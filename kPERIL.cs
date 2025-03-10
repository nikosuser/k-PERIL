﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace kPERIL_DLL
{  
    public class kPERIL
    {
        private List<int[,]> _allBoundaries;
        private int[] _rasterSize;
        private PerilData _data;
        private bool _debug;
        
        /// <summary>
        /// Constructor for kPERIL
        /// </summary>
        public kPERIL(bool debug) 
        {
            _rasterSize = new int[2];
            _data = new PerilData(this);
            _debug = debug;
        }

        /// <summary>
        /// This method represents one iteration.
        /// </summary>
        /// <param name="cellSize">The pixel size of all rasters (most commonly 30m)</param>
        /// <param name="rset">RSET time in minutes</param>
        /// <param name="bufferTime"> Any additional buffer time desired on top of RSET</param>
        /// <param name="midFlameWindspeed">The wind speed in the midflame height, raster (spatial, depending on the fire arrival time)</param>
        /// <param name="windDir">Midflame wind direction raster (spatial, depending on the fire arrival time).</param>
        /// <param name="wuiArea">An X by 2 array listing points defining a polygon. This polygon is used as the urban area around which the boundary is calculated.The dimensions of each point are about the domain with (0,0) being the top left corner. The WUI area must be defined by a single complete polygon, and the points should be ordered (clockwise or counterclockwise). Alternatively, it can be a list of the poinds on the periphery of the WUI area. Specify which array you select using the isEdgePointList boolean.</param>
        /// <param name="isEdgePointList">True if the wuiArea array is of individual polygon vertices, false if it is a list of all the points in the WUI area boundary.</param>
        /// <param name="ros">The rate of spread magniture array of size X by Y, in meters per minute</param>
        /// <param name="azimuth">The rate of spread direction array of size X by Y, in degrees from north, clockwise</param>
        /// <param name="slope">The slope of the terrain in degrees</param>
        /// <param name="aspect">The aspect of the terrain in degrees</param>
        /// <param name="consoleOutput">Optional, used to capture any console messages from k-PERIL.</param>
        /// <returns>An X by Y array representing the landscape. Points are 1 if inside the boundary and 0 if outside.</returns>
        public int[,] CalculateBoundary(float cellSize, float rset, float bufferTime, float[,] midFlameWindspeed, float[,] windDir, int[,] wuiArea, bool isEdgePointList, float[,] ros, float[,] azimuth, float[,] slope, float[,] aspect, System.IO.StringWriter consoleOutput = null)
        {
            //if we call from a non console program we need to be able to access the log/error messages
            if(consoleOutput != null)
            {
                Console.SetOut(consoleOutput);
                Console.SetError(consoleOutput);
            }

            int yDim = azimuth.GetLength(0);
            int xDim = azimuth.GetLength(1);

            _rasterSize[0] = xDim;
            _rasterSize[1] = yDim;

            _data.SetData(cellSize, rset + bufferTime, midFlameWindspeed, windDir, ros, azimuth, slope,aspect, xDim, yDim);

            int[,] wui;
            if (isEdgePointList)
            {
                //get the boundary points of the WUI area only
                wui = _data.CompoundBoundary(wuiArea);  
            }
            else
            {
                wui = wuiArea;
            }
            
            
            //check the WUI points are in the fire raster, and the fire reaches the WUI points. The returned array is in 1D coordinates. 
            int[] wuInput = _data.CheckOutOfBounds(wui);
            
            int[,] safetyMatrix = _data.CalculateTriggerBoundary(wuInput);        

            if(consoleOutput != null)
            {
                // Recover the standard output stream
                System.IO.StreamWriter standardOutput = new System.IO.StreamWriter(Console.OpenStandardOutput());
                standardOutput.AutoFlush = true;
                Console.SetOut(standardOutput);
                Console.SetError(standardOutput);
            }

            if (_debug)
            {
                _data.DebugExport("D:\\OneDrive - Imperial College London\\Desktop\\kPerilTest/debug/");
            }
            
            return safetyMatrix;           
        }
        /// <summary>
        /// Method to directly set rosTheta, the rate of spread direction of each point, in relation to its eight neighbors. The order is clockwise starting from North
        /// </summary>
        /// <param name="rosTheta">The N by 8 ROS list.</param>
        public void setRosTheta(float[,] rosTheta)
        {
            setRosTheta(rosTheta);
        }

        /// <summary>
        /// The main callable method of k-PERIL.This method calculates multiple iterations and saves them within the object.
        /// </summary>
        /// <param name="cellSize"> The pixel size of all matrices (most commonly 30m) for each simulation</param>
        /// <param name="rset"> An array of the prescribed evacuation time, in minutes</param>
        /// <param name="bufferTime"> An array of any additional buffer time desired on top of rset</param>
        /// <param name="midFlameWindspeed">An array of the wind speed in the midflame height raster (spatial, depending on the fire arrival time)</param>
        /// <param name="windDir">Array of midflame wind direction raster (spatial, depending on the fire arrival time)</param>
        /// <param name="wuiArea">An X by 2 array listing points defining a polygon. This polygon is used as the urban area around which the boundary is calculated.The dimensions of each point are about the domain with (0,0) being the top left corner. The WUI area must be defined by a single complete polygon, and the points should be ordered (clockwise or counterclockwise). Alternatively, it can be a list of the poinds on the periphery of the WUI area. Specify which array you select using the isEdgePointList boolean.</param>
        /// <param name="isEdgePointList">True if the wuiArea array is of individual polygon vertices, false if it is a list of all the points in the WUI area boundary.</param>
        /// <param name="ros">A jagged array of The rate of spread magniture array of size X by Y, in meters per minute</param>
        /// <param name="azimuth">A jagged array of The rate of spread direction array of size X by Y, in degrees from north, clockwise</param>
        /// <param name="slope">Slope of the terrain in degrees</param>
        /// <param name="aspect">Aspect of the terrain in degrees</param>
        /// <param name="consoleOutput">Optional, used to capture any console messages from k-PERIL.</param>
        /// <returns>A list of X by Y arrays representing the landscape. Points in each array are 1 if inside the boundary and 0 if outside.</returns>
        public List<int[,]> CalculateMultipleBoundaries(float cellSize, float[] rset, float[] bufferTime, float[][,] midFlameWindspeed, float[][,] windDir, int[,] wuiArea, bool isEdgePointList, float[][,] ros, float[][,] azimuth, float[,] slope, float[,] aspect, System.IO.StringWriter consoleOutput = null)
        {
            _allBoundaries = new List<int[,]>();

            for (int i=0; i<ros.Length; i++)
            {
                int[,] boundary = CalculateBoundary(cellSize, rset[i], bufferTime[i], midFlameWindspeed[i], windDir[i],
                    wuiArea, isEdgePointList, ros[i], azimuth[i], slope, aspect, consoleOutput);
                _allBoundaries.Add(boundary);
            }

            return _allBoundaries;
        }

        /// <summary>
        /// Sums up all the boundaries calculated in calcMultipleBoundaries.
        /// </summary>
        /// <returns>An X by Y array representing the domain</returns>
        public int[,] GetProbabilityBoundary()
        {
            if(_allBoundaries == null)
            {
                return null;
            }

            int[,] output = new int[_rasterSize[0], _rasterSize[1]];
            foreach (int[,] boundary in _allBoundaries)
            {
                for (int i = 0; i < _rasterSize[0]; i++)
                {
                    for (int j = 0; j < _rasterSize[1]; j++)
                    {
                        output[i, j] += boundary[i, j];
                    }
                }
            }
            return output;
        }

        /// <summary>
        /// Getter for the list containing all the calculated boundaries
        /// </summary>
        /// <returns></returns>
        public List<int[,]> GetIndividualBoundaryList()
        {
            return _allBoundaries;
        }
        
        /// <summary>
        /// Method to get the overall boundary line from a probabilistic boundary. Generalisable to get the boundary of a raster with a defined area. 
        /// </summary>
        /// <param name="compoundBoundary">The 2D raster of the probabilistic trigger boundary</param>
        /// <returns>A coordinates list of X by 2 of the points on the line boundary </returns>
        public static int[,] GetOutermostLineBoundary(int[,] compoundBoundary)
        {
            List<int> uniqueBoundary = new List<int>();

            for (int x = 1; x < compoundBoundary.GetLength(0)-1; x++)
			{
                for (int y = 1; y < compoundBoundary.GetLength(1)-1; y++)
			    {
                    if (compoundBoundary[x, y] != 0)
                    {
                        if (compoundBoundary[x+1, y]==0||compoundBoundary[x+1, y-1]==0||compoundBoundary[x, y-1]==0||compoundBoundary[x-1, y] == 0) 
                        {
                            uniqueBoundary.Add(x * compoundBoundary.GetLength(1) + y);
                        }
                    }
			    }
			}

            int[,] output = new int[uniqueBoundary.Count, 2]; 
            int count = 0;

            foreach (int boundaryPoint in uniqueBoundary)                         
            {
                output[count, 0] = (boundaryPoint % compoundBoundary.GetLength(1))+1;   
                output[count, 1] = (boundaryPoint / compoundBoundary.GetLength(1))+1;
                count++;
            }

            //return the new boundary matrix, should only include the edge nodes
            return output;   

        }

        /// <summary>
        /// Function that finds all points defining the perimeter of a polygon. Used to find all points of the WUI area boundary
        /// </summary>
        /// <param name="endNodes"> Array of X by 2 representing the coordinates of the polygon nodes</param>
        /// <returns>Array of Y by 2 of all the points in the perimeter of the polygon</returns>
        public int[,] GetPolygonEdgeNodes(int[,] endNodes)
        {
            int noNodes = endNodes.GetLength(0);

            List<int[]> allNodes = new List<int[]>();
            for (int i = 0; i < noNodes-1; i++)
            {
                int[,] endPair = new int[,] { { endNodes[i,0], endNodes[i, 1] }, { endNodes[i+1, 0], endNodes[i+1, 1] } };

                int[,] oneEdge = GetAllNodesBetween(endPair);

                for (int j = 0; j < oneEdge.GetLength(0); j++)
                {
                    int[] interPoints = new int[2] { oneEdge[j, 0], oneEdge[j, 1] };
                    allNodes.Add(interPoints);
                }
            }
            int[,] finalEndPair = new int[,] { { endNodes[noNodes-1, 0], endNodes[noNodes-1, 1] }, { endNodes[0, 0], endNodes[0, 1] } };

            int[,] finalOneEdge = GetAllNodesBetween(finalEndPair);

            for (int j = 0; j < finalOneEdge.GetLength(0); j++)
            {
                int[] interPoints = new int[2] { finalOneEdge[j, 0], finalOneEdge[j, 1] };
                allNodes.Add(interPoints);
            }
            return Get2DarrayFromIntList(allNodes);
        }
        /// <summary>
        /// Find all integer-coordinate points between two points
        /// </summary>
        /// <param name="endNodes"> 2 x 2 array containing the coordinates of the start and end point</param>
        /// <returns>2D array of all integer coordinate points between the stard and end points.</returns>
        private int[,] GetAllNodesBetween(int[,] endNodes)
        {
            double x1 = endNodes[0, 0];
            double y1 = endNodes[0, 1];
            double x2 = endNodes[1, 0];
            double y2 = endNodes[1, 1];

            List<int[]> allNodes = new List<int[]>();

            int[] tempNode = new int[2] { (int)x1, (int)y1 };
            allNodes.Add(tempNode);
            tempNode = new int[2] { (int)x2, (int)y2 };
            allNodes.Add(tempNode);

            int tempY = (int)y1;
            int tempX = (int)x1;
            
            if (Math.Abs(y2 - y1) < Math.Abs(x2 - x1))   //along X
            {
                int i = 0;
                double slope = (y2 - y1) / (x2 - x1);
                for (int count = 1; count < Math.Abs(x2 - x1) + 1; count++)
                {
                    i = (int)(count * (double)(x2 - x1) / (double)Math.Abs(x2 - x1));
                    double errorTop = Math.Abs(tempY + 1 - (slope * i + y1));
                    double errorCenter = Math.Abs(tempY - (slope * i + y1));
                    double errorBottom = Math.Abs(tempY - 1 - (slope * i + y1));

                    if (errorTop < errorCenter && errorTop < errorBottom)
                    {
                        tempNode = new int[] { (int)x1 + i, (int)tempY + 1 };
                        tempY++;
                        allNodes.Add(tempNode);
                    }
                    else if ((errorCenter < errorBottom && errorCenter < errorTop)||errorTop == errorCenter|| errorBottom == errorCenter)
                    {
                        tempNode = new int[] { (int)x1 + i, (int)tempY };
                        allNodes.Add(tempNode);
                    }
                    else if (errorBottom < errorTop && errorBottom < errorCenter)
                    {
                        tempNode = new int[] { (int)x1 + i, (int)tempY - 1 };
                        tempY--;
                        allNodes.Add(tempNode);
                    }
                    else
                    {
                        Console.WriteLine($"Failed to calculate all nodes between points {x1}, {y1} and {x2}, {y2}");
                        return endNodes;
                    }
                }
            }
            else
            {
                int i = 0;
                double inverseSlope = (x2 - x1) / (y2 - y1);
                for (int count = 1; count < Math.Abs(y2 - y1) + 1; count++)
                {
                    i = (int)(count * (double)(y2 - y1) / (double)Math.Abs(y2 - y1));
                    double errorTop = Math.Abs(tempX + 1 - (inverseSlope * i + x1));
                    double errorCenter = Math.Abs(tempX - (inverseSlope * i + x1));
                    double errorBottom = Math.Abs(tempX - 1 - (inverseSlope * i + x1));

                    if (errorTop < errorCenter && errorTop < errorBottom)
                    {
                        tempNode = new int[] { (int)tempX + 1, (int)y1 + i };
                        tempX++;
                        allNodes.Add(tempNode);
                    }
                    else if ((errorCenter < errorBottom && errorCenter < errorTop) || errorTop == errorCenter || errorBottom == errorCenter)
                    {
                        tempNode = new int[] { (int)tempX, (int)y1 + i };
                        allNodes.Add(tempNode);
                    }
                    else if (errorBottom < errorTop && errorBottom < errorCenter)
                    {
                        tempNode = new int[] { (int)tempX - 1, (int)y1 + i };
                        tempX--;
                        allNodes.Add(tempNode);
                    }
                    else
                    {
                        Console.WriteLine($"Failed to calculate all nodes between points {x1}, {y1} and {x2}, {y2}");
                        return endNodes;
                    }
                }
            }
            return Get2DarrayFromIntList(allNodes);
        }
        /// <summary>
        /// Convert List of 2 element arrays to 2D array of points. 
        /// </summary>
        /// <param name="list">List of point coordinates (2 element arrays).</param>
        /// <returns>X by 2 array of point coordinates </returns>
        private int[,] Get2DarrayFromIntList(List<int[]> list)
        {
            int[][] jaggedArray = list.Distinct().ToArray();
            int[,] output = new int[jaggedArray.GetLength(0), 2];

            for (int i = 0; i < jaggedArray.GetLength(0); i++)
            {
                output[i, 0] = jaggedArray[i][0];
                output[i, 1] = jaggedArray[i][1];
            }
            return output;
        }

        /// <summary>
        /// Use the weather station temporal wind data and the fire arrival time to specify the wind in each point in the landscape, when the fire goes through it. Also interpolates between hourly weather values. 
        /// </summary>
        /// <param name="windMag">Wind magnitude array</param>
        /// <param name="windDir">Wind direction array</param>
        /// <param name="rawsTimes">DateTimes of wind readings of previous arrays</param>
        /// <param name="arrivalTime">Fire arrival time raster</param>
        /// <returns>Wind magnitude and wind direction rasters</returns>
        public (float[,], float[,]) ConvertTemporalToSpatialWind(float[] windMag, float[] windDir, DateTime[] rawsTimes,
            float[,] arrivalTime)
        {
            float[,] windMagRaster = new float[arrivalTime.GetLength(0), arrivalTime.GetLength(1)];
            float[,] windDirRaster = new float[arrivalTime.GetLength(0), arrivalTime.GetLength(1)];
            for (int i = 0; i < arrivalTime.GetLength(0); i++)
            {
                for (int j = 0; j < arrivalTime.GetLength(1); j++)
                {
                    if (arrivalTime[i, j] >= 0)
                    {
                        int count = 0;
                        double timeDiff = 200;
                        while (timeDiff < 60)
                        {
                            timeDiff = (rawsTimes[count] - rawsTimes[0]).TotalMinutes - arrivalTime[i, j];
                            count++;
                        }
                        windMagRaster[i, j] =
                            windMag[count] * (float)(timeDiff / 60) + windMag[count + 1] * (float)(1 - timeDiff / 60);
                        windDirRaster[i, j] =
                            windDir[count] * (float)(timeDiff / 60) + windDir[count + 1] * (float)(1 - timeDiff / 60);
                    }
                }
            }
            return (windMagRaster, windDirRaster);
        }
        
        
    }
}