using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace kPERIL_DLL
{
    internal class PERILData
    {
        private float[,] _azimuth;               //Rate of spread Direction
        private float[,] _ros;                   //Rate of Spread Magnitude
        private float[,] _slope;                //terrain slope in deg 0 - 100
        private float[,] _aspect;                   //terrain aspect in deg 0 - 360
        private float[,] _rosTheta;              //rate of spread split onto the eight cardinal directions
        private float[,] _effectiveMidflameWindspeed; //Effective windspeed for ellipse calculations (accounts for wind and slope)
        private float[,] _u;                        //Mid-flame wind speed (spatial)
        private float[,] _windDir;               //wind direction (spatial)
        private int _totalY;                     //Total raster size in Y
        private int _totalX;                     //Total raster size in X
        private int[] nonBoundaryPoints;                     //linearised array of all the non-boundary nodes
        private float _cell;                     //cell size (m)
        private float _triggerBuffer;            //trigger buffer time in minutes
        private int[,] pointNeightborSet;                  //tracks the cardinal neighbors of each linearised cell.
        private float[,] _graph;                 //variable to store the weight values for the target node
        private bool _haveData = false;
        private kPERIL _perilOwner;

        public PERILData(kPERIL perilOwner)
        {
            _perilOwner = perilOwner;
        }

        public void SetData(float cell, float RSET, float[,] u, float[,] windDir, float[,] ros, float[,] azimuth, float[,] slope, float[,] aspect, int totalX, int totalY)
        {
            _cell = cell;
            _triggerBuffer = RSET;
            _u = u;
            _windDir = windDir;
            _ros = ros;
            _azimuth = azimuth;
            _slope = slope;
            _aspect = aspect;
            _totalX = totalX;
            _totalY = totalY;

            _haveData = true;
            
            
        }

        public int[,] CalculateTriggerBoundary(int[] wuInput)
        {
            if (_haveData)
            {
                GetEffectiveWindWithSlope(); 
                
                CalculateRosTheta();
                //Console.WriteLine("ROS_THETA GENERATED");                                       

                SetNonBoundaryPoints();
                //Console.WriteLine("ROSn GENERATED");                                           

                SetPointNeighborSet();
                //Console.WriteLine("ROSLOC GENERATED");                                                                                                                      

                SetWeight();
                //Console.WriteLine("WEIGHTS GENERATED");

                int[,] triggerBoundary = GetTriggerBoundary(wuInput);
                Console.WriteLine("Trigger boundary calculated");

                return triggerBoundary;
            }

            return null;
        }

        /// <summary>
        /// Calculate Rate of Spread to each of the 8 cardinal directions. This uses the assumption that fire spreads in an ellipse from a point source, depending on the midflame windspeed. 
        /// </summary>
        private void CalculateRosTheta()
        {
            int totalX = _azimuth.GetLength(0);   
            int totalY = _azimuth.GetLength(1);   

            float a, b, c;
            double rosX, rosY;
            float[,] roStheta = new float[totalX * totalY, 8];

            int linearIndex = 0; //create linearisation index variable

            //the code converts the raster from an X x Y raster to a X*Y x 1 linear array of elements. This makes further calculations easier as the resulting network of nodes is just a list of linear points.

            for (int x = 0; x < totalX; x++)
            {
                for (int y = 0; y < totalY; y++)
                {
                    double lb = 0.936 * Math.Exp(0.2566 * this._effectiveMidflameWindspeed[x,y]) + 0.461 * Math.Exp(-0.1548 * _effectiveMidflameWindspeed[x,y]) - 0.397; //Calculate length to Breadth ratio of Huygens ellipse
                    double hb = (lb + (float)Math.Sqrt(lb * lb - 1)) / (lb - (float)Math.Sqrt(lb * lb - 1));    //calculate head to back ratio
                    
                    a = (_ros[x, y] / (2 * (float)lb)) * (1 + 1 / (float)hb);
                    b = (_ros[x, y] * 0.5f) * (1 + 1 / (float)hb);
                    c = (_ros[x, y] * 0.5f) * (1 - 1 / (float)hb);

                    for (int cardinal = 0; cardinal < 8; cardinal++)
                    {
                        //if the cell is active i.e. has been burned in the simulation
                        if (_azimuth[y, x] >= 0)
                        {
                            rosX = a * Math.Sin((Math.PI * cardinal / 4) - _azimuth[y, x] * 2 * Math.PI / 360);
                            rosY = c + b * Math.Cos((Math.PI * cardinal / 4) - _azimuth[y, x] * 2 * Math.PI / 360);
                            roStheta[linearIndex, cardinal] = (float)Math.Sqrt(Math.Pow(rosX, 2) + Math.Pow(rosY, 2));
                        }
                        else
                        {
                            roStheta[linearIndex, cardinal] = 0;
                        }
                    }
                    linearIndex++;
                }
            }
            _rosTheta = roStheta;
        }

        /// <summary>
        /// Calculate a list of all the non-boundary nodes. Boundary nodes do not have 8 neighbors and complicate the rest of the algorithm. As such only internal nodes are used, and boundary nodes are only used as "neighbors" for calculations
        /// </summary>
        private void SetNonBoundaryPoints()
        {
            nonBoundaryPoints = new int[(_totalX - 2) * (_totalY - 2)];
            int index = 0;
            for (int i = 1; i < _totalX - 1; i++)
            {
                for (int j = 1; j < _totalY - 1; j++)
                {
                    //add it to the list  
                    nonBoundaryPoints[index] = LinearisePoint(new int[] {i,j});
                    ++index;
                }
            }

            //bran-jnw: replaced this
            /*List<int> rosN = new List<int>();               //create a new list for rosN

            for (int i = 1; i < _totalX-1; i++)              //for the X and Y dimensions
            {
                for (int j = 1; j < _totalY-1; j++)
                {
                    rosN.Add(i * _totalY + j);               //add it to the list                        
                }
            }
            //return rosN as an array
            nonBoundaryPoints=rosN.ToArray();   */
        }

        /// <summary>
        /// Calculate a catalog of the neighbors of each node. nOrientation is same as ROS cardinal direction, starts from North and moves clockwise.
        /// </summary>
        private void SetPointNeighborSet()
        {
            //create output variable
            int[,] pointNeighborSet = new int[nonBoundaryPoints.Max() + 1, 8];

            //for every element is rosn calculate and catalog its linearised neighbor
            for (int i = 0; i < nonBoundaryPoints.Length; i++)
            {
                //North
                pointNeighborSet[nonBoundaryPoints[i], 0] = nonBoundaryPoints[i] - 1;
                //NE
                pointNeighborSet[nonBoundaryPoints[i], 1] = nonBoundaryPoints[i] + _totalY - 1;
                //east
                pointNeighborSet[nonBoundaryPoints[i], 2] = nonBoundaryPoints[i] + _totalY;
                //SE
                pointNeighborSet[nonBoundaryPoints[i], 3] = nonBoundaryPoints[i] + _totalY + 1;
                //South
                pointNeighborSet[nonBoundaryPoints[i], 4] = nonBoundaryPoints[i] + 1;
                //SW
                pointNeighborSet[nonBoundaryPoints[i], 5] = nonBoundaryPoints[i] - _totalY + 1;
                //west
                pointNeighborSet[nonBoundaryPoints[i], 6] = nonBoundaryPoints[i] - _totalY;
                //NW          
                pointNeighborSet[nonBoundaryPoints[i], 7] = nonBoundaryPoints[i] - _totalY - 1;
            }

            pointNeightborSet = pointNeighborSet;
        }

        /// <summary>
        /// Calculate the weight variable. contains the "Weight" between each node and its 8 neighbors. Weight is equivalent to travel time of the fire from the target point to each of its neighbors. 
        /// </summary>
        private void SetWeight()
        {
            float[,] weight = new float[nonBoundaryPoints.Max() + 1, 8];

            for (int i = 0; i < nonBoundaryPoints.Length; i++)
            {
                int point = nonBoundaryPoints[i];
                for (int j = 0; j < 8; j++)
                {
                    //weighting is the average of the inverses of the ROS of the neighboring points. If we are for example examining a point and its north neighbor,
                    //we are averaging the inverses of the ROS directions towards the north (not the south, since we are creating an inverse weight matrix technically)
                    if (_rosTheta[point, j] != 0 && _rosTheta[pointNeightborSet[point, j], j] != 0)
                    {
                        //if the point is N S E or W
                        if (j % 2 == 0)
                        {
                            weight[point, j] = (_cell / 2) * ((1 / _rosTheta[point, (j + 4) % 8]) + (1 / _rosTheta[pointNeightborSet[point, j], (j + 4) % 8]));
                        }
                        //if the point is a corner node (have to account for a longer distance)
                        else
                        {
                            weight[point, j] = 1.4142f * (_cell / 2) * ((1 / _rosTheta[point, (j + 4) % 8]) + (1 / _rosTheta[pointNeightborSet[point, j], (j + 4) % 8]));
                        }
                    }
                }
            }
            _graph = weight;
        }

        /// <summary>
        /// Create the boundary of the PERIL area. All points with fire travel time less than the RSET time are given a value of 1. 
        /// </summary>
        /// <param name="weightList">Raster of the fire travel time (how long the fire will take to reach the WUI area from that point)</param>
        /// <returns>The trigger boundary (all points with fire travel time smaller than RSET)</returns>
        public int[] GetBoundary(float[,] weightList)
        {
            List<int> boundary = new List<int>();

            for (int i = 0; i < weightList.GetLength(1); i++)
            {
                for (int j = 0; j < weightList.GetLength(0); j++)
                {
                    if (weightList[j, i] <= _triggerBuffer && weightList[j, i] > 0)
                    {
                        boundary.Add(j);
                    }
                }
            }
            return boundary.Distinct().ToArray();
        }

        /// <summary>
        /// Function to find all the nodes that form the boundary of a raster area. 
        /// </summary>
        /// <param name="rasterArea">A 2D array of raster coordinates, of all the points inside the area. </param>
        /// <returns>A 2D array of coordinates of all the boundary points of the input area. </returns>
        public int[,] CompoundBoundary(int[,] rasterArea)
        {
            int[,] area = new int[_totalX, _totalY];
            List<int[]> boundary = new List<int[]>();
            for (int i = 0; i < rasterArea.GetLength(0); i++)
            {
                area[rasterArea[i, 0], rasterArea[i, 1]] = 1;
            }

            for (int i = 0; i < _totalX; i++)
            {
                for (int j = 0; j < _totalY; j++)
                {
                    if (area[i, j] == 1 && (area[i + 1, j] == 0 || 
                                            area[i - 1, j] == 0 || 
                                            area[i, j + 1] == 0 ||
                                            area[i, j - 1] == 0))
                    {
                        boundary.Add(new[] { i, j });
                    }
                }
            }
            
            int[,] output = new int[boundary.Count, 2];
            for (int i = 0; i < boundary.Count; i++)
            {
                output[i, 0] = boundary[i][0];
                output[i, 1] = boundary[i][1];
            }
            return output;
        }

        /// <summary>
        /// Check if a WUI point has been reached by the wildfire. If it has not, it will be moved to the closest point affected by the fire.
        /// </summary>
        /// <param name="wuIx">WUI point X coord</param>
        /// <param name="wuIy">WUI point Y coord</param>
        /// <param name="minDistance">The minimum distance of all points to the burned area of the wildfire</param>
        /// <returns>An altered WUI point, possibly moved to be inside the burned area</returns>
        private int[] CheckFireOutOfBounds(int wuIx, int wuIy, out float minDistance)
        {
            int[] newWui = { wuIx, wuIy };

            if (_ros == null)
            {
                Console.WriteLine("Fire out of bounds check does not work when ROS raster is not defined. Returning input array.");
                minDistance = 0f;
                return newWui;
            }
            minDistance = int.MaxValue;
            
            //if the WUI is on an inactive note
            if (_ros[wuIx, wuIy] < 0)
            {
                float tryout = int.MaxValue;

                for (int i = 0; i < _ros.GetLength(0); i++) 
                {
                    for (int j = 0; j < _ros.GetLength(1); j++)
                    {
                        if (_ros[i, j] >= 0)
                        {
                            tryout = (float)Math.Sqrt(Math.Pow(Math.Abs(i - wuIx), 2) + Math.Pow(Math.Abs(j - wuIy), 2));
                            if (minDistance > tryout)
                            {
                                minDistance = tryout;      
                                newWui[0] = j;              
                                newWui[1] = i;
                            }
                        }
                    }
                }
            }
            else
            {
                minDistance = 0;
            }
            //return new WUI area. If WUI was originally on an active node then no change occurs, it returns the original WUI point
            return newWui;
        }

        /// <summary>
        /// Check if the WUI point is outside the simulation raster
        /// </summary>
        /// <param name="wuIx">X coordinate of point</param>
        /// <param name="wuIy">Y coordinate of point</param>
        /// <exception cref="Exception">The point is outside the raster boundary</exception>
        private void CheckGeneralOutOfBounds(int wuIx, int wuIy)
        {
            if (wuIx > _totalX || wuIy > _totalY || wuIx < 0 || wuIy < 0)    
            {
                //throw new exception
                throw new Exception($"ERROR: Point {wuIx}, {wuIy} is out of bounds with raster size {_totalX}, {_totalY}");                        
            }
        }

        /// <summary>
        /// Method to turn an array of 2D Matrix points to their 1D linearised form
        /// </summary>
        /// <param name="wui">X by 2 array of points coordinates</param>
        /// <returns>Array of the same points in 1D coordinates</returns>
        private int[] LineariseArray(int[,] wui)          
        {
            int[] output = new int[wui.GetLength(0)];
            for (int i = 0; i < wui.GetLength(0); i++)      
            {
                //1D coordinates start from top left of the raster, increasing with Y and looping with each Column. 
                output[i] = LinearisePoint(new int[] {wui[i, 0], wui[i, 1]});    
            }
            return output;
        }
        /// <summary>
        /// Isolated function to linearise a point. Makes it easier to change the algorithm if need be
        /// </summary>
        /// <param name="wui">2D coordinates of point</param>
        /// <returns>1D Coordinates of point</returns>
        private int LinearisePoint(int[] wui)
        {
            return  wui[0] * _totalY + wui[1];
        }

        /// <summary>
        /// Method to turn an array of 1D linearised coordinate points back to their 2D form
        /// </summary>
        /// <param name="wui">1D point coordinates</param>
        /// <returns>Array of the same points in 2D coordinates</returns>
        private int[,] DelineariseArray(int[] wui)
        {
            int[,] output = new int[wui.Length, 2];
            for (int i = 0; i < wui.Length; i++)
            {
                int[] delinearised = DelinearisePoint(wui[i]);
                output[i, 0] = delinearised[0];            
                output[i, 1] = delinearised[1];
            }
            return output;
        }
        
        /// <summary>
        /// Isolated function to delinearise a point. Makes it easier to change the algorithm if need be
        /// </summary>
        /// <param name="wui">1D coordinates of point</param>
        /// <returns>2D Coordinates of point</returns>
        private int[] DelinearisePoint(int wui)
        {
            return new int[] { (int)wui/ _totalY, wui % _totalY };
        }
        
        /// <summary>
        /// BFS algorithm. Recursive with stop condition. Finds the time for the fire to reach the WUI area. 
        /// </summary>
        /// <param name="wui">1D dimension of target WUI point</param>
        /// <returns>Time for fire to reach WUI point for all points in the raster</returns>
        private float[] BreadthFirstSearch(int wui)
        {
            int totalNodes = _graph.GetLength(0);
            Queue<int> upNext = new Queue<int>();
            float[] distance = new float[totalNodes];
            bool[] enqueued = new bool[totalNodes];  // Tracks whether a node has been enqueued

            // Initialize distances and enqueued flags
            for (int i = 0; i < totalNodes; i++)
            {
                distance[i] = float.MaxValue;
                enqueued[i] = false;
            }

            // Optionally, you might want to set the starting node's distance to 0 instead of 1,
            // unless 1 is meaningful for your application.
            upNext.Enqueue(wui);
            enqueued[wui] = true;
            distance[wui] = 1;

            while (upNext.Count > 0)
            {
                int currentNode = upNext.Dequeue();  // Remove and get the current node

                // Skip processing if the node is on the boundary
                if (IsOnBoundary(currentNode))
                    continue;

                // Loop over all possible neighbors (using the second dimension of _graph)
                for (int i = 0; i < _graph.GetLength(1); i++)
                {
                    int neighbor = pointNeightborSet[currentNode, i];
                    if (neighbor == 0)
                        continue;  // Skip invalid neighbor entries

                    // Ensure the edge weight is valid
                    if (_graph[currentNode, i] <= 0)
                        continue;

                    // Calculate the new distance from the start node
                    float newDistance = distance[currentNode] + _graph[currentNode, i];

                    // If we found a shorter path, update the distance.
                    if (newDistance < distance[neighbor])
                    {
                        distance[neighbor] = newDistance;

                        // If the neighbor meets your trigger conditions and isn't already queued, enqueue it.
                        // (Note: Adjust _triggerBuffer and _ros as needed for your logic.)
                        if (newDistance <= _triggerBuffer &&
                            !enqueued[neighbor] &&
                            _ros[(int)(neighbor / _ros.GetLength(0)), neighbor % _ros.GetLength(0)] > 0)
                        {
                            upNext.Enqueue(neighbor);
                            enqueued[neighbor] = true;
                        }
                    }
                }
            }

            return distance;
        }


        /// <summary>
        /// Check that point is on the boundary of the simulation raster
        /// </summary>
        /// <param name="node">1D dimension of the target point</param>
        /// <returns>Bool True if point is on boundary</returns>
        private bool IsOnBoundary(int node)
        {
            int[] coords = DelinearisePoint(node);

            if (coords[0] <= 1 || coords[1] <= 1)
            {
                return true;
            }

            if (coords[0] >= _totalX - 2 || coords[1] >= _totalY - 2)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// The final step of the PERIL algorithm, use a Breadth First Search pattern to find the trigger boundary.
        /// </summary>
        /// <param name="wuInput">The linearised WUI point coordinates</param>
        /// <returns>a 2D coordinate array of the points inside the trigger boundary.</returns>
        private int[,] GetTriggerBoundary(int[] wuInput)
        {
            float[,] allDistances = new float[_graph.GetLength(0), wuInput.Length];

            for (int i = 0; i < wuInput.Length; i++)
            {
                Console.Write("\r Generating Boundary: WUI Nodes Complete: {0}%", i * 100 / wuInput.Length);

                float[] temp = BreadthFirstSearch(wuInput[i]);
                for (int j = 0; j < temp.Length; j++)
                {
                    allDistances[j, i] = temp[j];
                }
            }
            
            int[,] safetyMatrix = new int[_totalX, _totalY];
            int[] safetyMatrix1D = GetBoundary(allDistances);

            for (int i = 0; i < safetyMatrix1D.Length; i++)
            {
                int[] coords = DelinearisePoint(safetyMatrix1D[i]);
                safetyMatrix[coords[0], coords[1]]++;
            }

            Console.WriteLine();
            return safetyMatrix;
        }

        /// <summary>
        /// Check the WUI boundary points are within the simulation area, and were reached by the fire. If the points are in the area but are not reached by the fire, they are moved to the nearest point that is reached by the fire. 
        /// </summary>
        /// <param name="wuiBoundary">2D array of X by 2 points, of the boundary of the WUI area</param>
        /// <returns>2D array of X by 2 points, possibly transformed to be within the fire area. </returns>
        public int[] CheckOutOfBounds(int[,] wuiBoundary)
        {
            //create the WUI variable (a new one to parse any kind of edit to it)
            int[] wuInput = new int[wuiBoundary.GetLength(0)];

            //create a temporary Out of bounds vector 
            int[] modifiedCoordinates = new int[2];
            
            float minDistance = int.MaxValue;

            //for all WUI points 
            for (int i = 0; i < wuiBoundary.GetLength(0); i++)
            {
                CheckGeneralOutOfBounds(wuiBoundary[i, 0], wuiBoundary[i, 1]);
                modifiedCoordinates = CheckFireOutOfBounds(wuiBoundary[i, 0], wuiBoundary[i, 1], out float tempDistance);

                if (minDistance > tempDistance)
                {
                    minDistance = tempDistance;
                }
                //Linearise WUI accordingly
                wuInput[i] = (modifiedCoordinates[0] - 1) * _totalY + modifiedCoordinates[1];
            }

            if (minDistance > _cell * 6)
            {
                throw new Exception(
                    "ERROR: The fire has a minimum distance to the WUI area greater than 6 cells worth. Either redo the wildfire simulation with more time or redraw the WUI area");
            }
            //return LineariseArray(wuiBoundary);
            return wuInput;
        }

        private void GetEffectiveWindWithSlope()
        {
            float[,] effectiveMidflameWindspeed = new float[_totalX, _totalY];
            for (int i = 0; i < _totalX; i++)
            {
                for (int j = 0; j < _totalY; j++)
                {
                    double effectiveSlopeWind = 0.06f * _slope[i, j];

                    // Convert degrees to radians
                    double radWindDir = _windDir[i,j] * Math.PI / 180.0;

                    // Convert polar coordinates to Cartesian (x, y)
                    double x2 = _u[i,j] * Math.Cos(radWindDir);
                    double y2 = _u[i,j] * Math.Sin(radWindDir);
                    
                    double radSlopeUpDir = (_aspect[i, j] + 180) * Math.PI / 180.0;
                    double x1 = effectiveSlopeWind * Math.Cos(radSlopeUpDir);
                    double y1 = effectiveSlopeWind * Math.Sin(radSlopeUpDir);
                    
                    double xResult = x1 + x2;
                    double yResult = y1 + y2;
                    
                    effectiveMidflameWindspeed[i,j] = (float)Math.Sqrt(xResult * xResult + yResult * yResult);
                    //double resultDirection = Math.Atan2(yResult, xResult) * 180.0 / Math.PI;
                }
            }
            _effectiveMidflameWindspeed= effectiveMidflameWindspeed;
        }
        public void exportRaster(float[,] raster, string filePath)
        {
            int rows = raster.GetLength(0);
            int cols = raster.GetLength(1);

            using (StreamWriter writer = new StreamWriter(filePath))
            {
                for (int i = 0; i < rows; i++)
                {
                    for (int j = 0; j < cols; j++)
                    {
                        writer.Write(raster[i, j].ToString(CultureInfo.InvariantCulture));

                        // Add space separator, except for the last column
                        if (j < cols - 1)
                            writer.Write(" ");
                    }
                    writer.WriteLine(); // New line after each row
                }
            }

            Console.WriteLine($"Matrix saved successfully to {filePath}");
        }

        public void debugExport(string outputFolder)
        {
            exportRaster(_rosTheta,outputFolder+"rosTheta.txt");
            exportRaster(_effectiveMidflameWindspeed,outputFolder+"effectiveMidflameWindspeed.txt");
            exportRaster(_u,outputFolder+"u.txt");
            exportRaster(_windDir,outputFolder+"windDir.txt");
            //exportRaster();
        }
    }   
}