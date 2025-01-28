using System;
using System.Collections.Generic;
using System.Linq;

namespace kPERIL_DLL
{  
    public class kPERIL
    {
        private List<int[,]> _allBoundaries;
        private int[] _rasterSize;
        private PERILData _data;

        public kPERIL() 
        {
            _rasterSize = new int[2];
            _data = new PERILData(this);
        }

        /// <summary>
        /// This method represents one iteration.
        /// </summary>
        /// <param name="cellSize">The square size of each raster (most commonly 30m)</param>
        /// <param name="triggerBuffer">Trigger buffer time in minutes</param>
        /// <param name="midFlameWindspeed">The wind speed in the midflame height, representing the entire domain (spatially and temporally)</param>
        /// <param name="wuiArea">An X by 2 array listing points defining a polygon. This polygon is used as the urban area around which the boundary is calculated.The dimensions of each point are about the domain with (0,0) being the top left corner. </param>
        /// <param name="ros">The rate of spread magniture array of size X by Y, in meters per minute</param>
        /// <param name="azimuth">The rate of spread direction array of size X by Y, in degrees from north, clockwise</param>
        /// <param name="consoleOutput">Optional, used to capture any consaole messages from k-PERIL.</param>
        /// <returns>An X by Y array representing the landscape. Points are 1 if inside the boundary and 0 if outside.</returns>
        public int[,] CalculateBoundary(float cellSize, int triggerBuffer, float midFlameWindspeed, int[,] wuiArea, float[,] ros, float[,] azimuth, System.IO.StringWriter consoleOutput = null)
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

            _data.SetData(cellSize, triggerBuffer, midFlameWindspeed, ros, azimuth, xDim, yDim);

            //Linearise the WUIarea array, get its boundary, and delinearise
            int[,] wui = _data.Delinearise(_data.CompoundBoundary(_data.Linearise(wuiArea)));     
            /*
            Console.Write("WUI Boundary Nodes: ");//Output the WUI area boundary generated in the Console
            for (int i = 0; i < WUI.GetLength(0); i++)
            {
                Console.Write(WUI[i, 0] + "," + WUI[i, 1] + "   ");
            }
            Console.WriteLine();
            */

            int[] wuInput = _data.CheckOutOfBounds(wui);

            bool noFire = false;

            for (int i = 0; i < wuInput.Length; i++)
            {
                if (wuInput[i].Equals(int.MaxValue))
                {
                    Console.WriteLine("FIRE DOES NOT SIGNIFICANTLY REACH THE AFFECTED AREA, PERIL WILL NOT TAKE THIS FIRE INTO ACCOUNT");
                    noFire = true;
                }
            }

            int[,] safetyMatrix = null;
            if (!noFire)
            {
                safetyMatrix = _data.CalculateTriggerBuffer(wuInput);                 
            }

            if(consoleOutput != null)
            {
                // Recover the standard output stream
                System.IO.StreamWriter standardOutput = new System.IO.StreamWriter(Console.OpenStandardOutput());
                standardOutput.AutoFlush = true;
                Console.SetOut(standardOutput);
                Console.SetError(standardOutput);
            }            

            return safetyMatrix;           
        }

        /// <summary>
        /// The main callable method of k-PERIL.This method calculates multiple iterations and saves them within the object.
        /// </summary>
        /// <param name="cellSize"> An array of The square size of each point (most commonly 30m) for each simulation</param>
        /// <param name="tBuffer"> An array ofThe prescribed evacuation time, in minutes</param>
        /// <param name="midFlameWindspeed">An array of The wind speed in the midflame height, representing the entire domain (spatially and temporally)</param>
        /// <param name="wuIarea">A jagged array of X by 2 array listing points defining a polygon. This polygon is used as the urban area around which the boundary is calculated.The dimensions of each point are about the domain with (0,0) being the top left corner. </param>
        /// <param name="ros">A jagged array of The rate of spread magniture array of size X by Y, in meters per minute</param>
        /// <param name="azimuth">A jagged array of The rate of spread direction array of size X by Y, in degrees from north, clockwise</param>
        /// <returns>An X by Y array representing the landscape. Points are 1 if inside the boundary and 0 if outside.</returns>
        public List<int[,]> CalculateMultipleBoundaries(float[] cellSize, int[] tBuffer, float[] midFlameWindspeed, int[][,] wuIarea, float[][,] ros, float[][,] azimuth)
        {
            _allBoundaries = new List<int[,]>();

            for (int i=0; i<cellSize.Length; i++)
            {
                int[,] boundary = CalculateBoundary(cellSize[i], tBuffer[i], midFlameWindspeed[i], wuIarea[i], ros[i], azimuth[i]);
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
                        output[i, j] = output[i, j] + boundary[i, j];
                    }
                }
            }
            return output;
        }

        /// <summary>
        /// Getter for the list containing all the calculated boundaries
        /// </summary>
        /// <returns></returns>
        public List<int[,]> GetBoundaryList()
        {
            return _allBoundaries;
        }

        public static int[,] GetLineBoundary(int[,] compoundBoundary)          //Method to get the boundary line from a safety matrix (unlike getCompoundBoundary which needs multiple safety matrices in one matrix to work)
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

            //For every boundary point, add its X and Y values to the output matrix
            foreach (int boundaryPoint in uniqueBoundary)                         
            {
                //Added the +1 since the indices in the cartesian system must start from 1
                output[count, 0] = (boundaryPoint % compoundBoundary.GetLength(1))+1;   
                output[count, 1] = (boundaryPoint / compoundBoundary.GetLength(1))+1;
                count++;
            }

            //return the new boundary matrix, should only include the edge nodes
            return output;   

        }

        public static int[,] GetDenseBoundaryFromProbBoundary(int[,] probBoundary, int noOfLines)
        {
            int[,] isolatedBoundary = new int[probBoundary.GetLength(0), probBoundary.GetLength(1)];

            int[,] outputBoundary = new int[probBoundary.GetLength(0), probBoundary.GetLength(1)];

            int minimumPass = probBoundary.Cast<int>().Max() / noOfLines;

            for (int i = 0; i < noOfLines; i++)
            {
                for (int j = 0; j < probBoundary.GetLength(0); j++)
                {
                    for (int k = 0; k < probBoundary.GetLength(1); k++)
                    {
                        if (probBoundary[j, k] > minimumPass * i)
                        {
                            isolatedBoundary[j, k] = probBoundary[j, k];
                        }
                        else
                        {
                            isolatedBoundary[j, k] = 0;
                        }
                    }
                }
            } 
            return outputBoundary;
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
                        Console.WriteLine("Something went wrong with the math");
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
                        Console.WriteLine("Something went wrong with the math");
                        return endNodes;
                    }
                }
            }

            return Get2DarrayFromIntList(allNodes);
        }

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

        internal static int[,] GetRasterBoundaryFromProbBoundary(int[,] safetyMatrix, float v)
        {
            throw new NotImplementedException();
        }
    }
}