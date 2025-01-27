using System;
using System.Collections.Generic;
using System.Linq;

namespace kPERIL_DLL
{
    internal class PERILData
    {
        private float[,] _azimuth;                 //Rate of spread magnitude
        private float[,] _ros;                   //Rate of Spread magnitude (coupled with azimuth)
        private float[,] _rosTheta;              //rate of spread split onto the eight cardinal directions
        private float _u;                        //Mid-flame wind speed
        private int _totalY;                     //Total raster size in Y
        private int _totalX;                     //Total raster size in X
        private int[] _rosN;                     //linearised array of all the non-boundary nodes
        private float _cell;                       //cell size (m)
        private int _triggerBuffer;                    //trigger buffer time in minutes
        private int[,] _rosloc;                  //tracks the cardinal neighbors of each linearised cell.
        private float[,] _graph;                 //variable to store the weight values for the target node
        private bool _haveData = false;

        public float[] DebugMatrix;             //debug purposes

        readonly static float sqrt2 = (float)Math.Sqrt(2.0);

        public void SetData(float cell, int triggerBuffer, float u, float[,] ros, float[,] azimuth, int totalX, int totalY)
        {
            _cell = cell;
            _triggerBuffer = triggerBuffer;
            _u = u;
            _ros = ros;
            _azimuth = azimuth;
            _totalX = totalX;
            _totalY = totalY;

            _haveData = true;
        }

        public int[,] CalculateTriggerBuffer(int[] wuInput)
        {
            if (_haveData)
            {
                //calculate Cardinal Direction ROS
                CalculateRosTheta();
                //Console.WriteLine("ROS_THETA GENERATED");                                       

                //Calculate ROSn
                SetRosN();
                //Console.WriteLine("ROSn GENERATED");                                           

                //Calculate Rosloc
                SetRosLoc();
                //Console.WriteLine("ROSLOC GENERATED");                                                                                                                      

                //Calculate Weights Matrix
                SetWeight();
                //Console.WriteLine("WEIGHTS GENERATED");

                int[,] safetyMatrix = GetSafetyMatrix(wuInput);
                Console.WriteLine("BOUNDARY GENERATED");

                return safetyMatrix;
            }

            return null;
        }

        /// <summary>
        /// Calculate ROS, includes rate of spread to all 8 cardinal directions
        /// </summary>
        public void CalculateRosTheta()
        {
            double lb = 0.936 * Math.Exp(0.2566 * _u) + 0.461 * Math.Exp(-0.1548 * _u) - 0.397; //Calculate length to Breadth ratio of Huygens ellipse
            double hb = (lb + (float)Math.Sqrt(lb * lb - 1)) / (lb - (float)Math.Sqrt(lb * lb - 1));    //calculate head to back ratio

            //bran-jnw: why are these flipped?
            int totalY = _azimuth.GetLength(0);   //get maximum y dimension
            int totalX = _azimuth.GetLength(1);   //get maximum x dimension

            //create variables for Huygens ellipse
            float a, b, c;
            double rosX, rosY;         //create variable ROSX for ROS calculation
            float[,] roStheta = new float[totalX * totalY, 8];  //create output matrix variable

            int linearIndex = 0; //create linearisation index variable

            //the code converts the raster from an X x Y raster to a X*Y x 1 linear array of elements. This makes further calculations easier as the resulting network of nodes is just a list of linear points.

            for (int x = 0; x < totalX; x++)    //for every element in the raster
            {
                for (int y = 0; y < totalY; y++)
                {
                    a = (_ros[y, x] / (2 * (float)lb)) * (1 + 1 / (float)hb);
                    b = (_ros[y, x] * 0.5f) * (1 + 1 / (float)hb);
                    c = (_ros[y, x] * 0.5f) * (1 - 1 / (float)hb);

                    //for every cardinal direction (starting from north and going clockwise)
                    for (int cardinal = 0; cardinal < 8; cardinal++)
                    {
                        //if the cell is active i.e. has been burned in the simulation
                        if (_azimuth[y, x] != -9999)
                        {
                            rosX = a * Math.Sin((Math.PI * cardinal / 4) - _azimuth[y, x] * 2 * Math.PI / 360);
                            rosY = c + b * Math.Cos((Math.PI * cardinal / 4) - _azimuth[y, x] * 2 * Math.PI / 360);
                            //Calculate ROS per cardinal direction
                            roStheta[linearIndex, cardinal] = (float)Math.Sqrt(Math.Pow(rosX, 2) + Math.Pow(rosY, 2));
                        }
                        else
                        {
                            //if the cell is inactive, set ros to zero
                            roStheta[linearIndex, cardinal] = 0;
                        }
                    }

                    linearIndex++;                                                  //advance the linear index
                }
            }
            _rosTheta = roStheta;                                                        //return completed matrix
        }

        /// <summary>
        /// Calculate RosN, a list of all the non-boundary nodes (boundary nodes do not have 8 neighbors and complicate the rest of the algorithm. As such only internal nodes are used, and boundary nodes are only used as "neighbors" for calculations
        /// </summary>
        private void SetRosN()
        {
            _rosN = new int[(_totalX - 1) * (_totalY - 1)];
            int index = 0;
            for (int i = 1; i < _totalX - 1; i++)
            {
                for (int j = 1; j < _totalY - 1; j++)
                {
                    //add it to the list  
                    _rosN[index] = i * _totalY + j;
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
            _rosN=rosN.ToArray();   */
        }

        /// <summary>
        /// Calculate RosLoc, a catalog of the neighbors of each node.Orientation is same as ROS cardinal direction, starts from North and moves clockwise.
        /// </summary>
        private void SetRosLoc()
        {
            //create output variable
            int[,] rosloc = new int[_rosN.Max() + 1, 8];

            //for every element is rosn calculate and catalog its linearised neighbor
            for (int i = 0; i < _rosN.Length; i++)
            {
                //North
                rosloc[_rosN[i], 0] = _rosN[i] - 1;
                //NE
                rosloc[_rosN[i], 1] = _rosN[i] + _totalY - 1;
                //east
                rosloc[_rosN[i], 2] = _rosN[i] + _totalY;
                //SE
                rosloc[_rosN[i], 3] = _rosN[i] + _totalY + 1;
                //South
                rosloc[_rosN[i], 4] = _rosN[i] + 1;
                //SW
                rosloc[_rosN[i], 5] = _rosN[i] - _totalY + 1;
                //west
                rosloc[_rosN[i], 6] = _rosN[i] - _totalY;
                //NW          
                rosloc[_rosN[i], 7] = _rosN[i] - _totalY - 1;
            }

            _rosloc = rosloc;
        }

        /// <summary>
        /// Calculate the weight variable. contains the "Weight" between each node and its 8 neighbors
        /// </summary>
        private void SetWeight()
        {
            float[,] weight = new float[_rosN.Max() + 1, 8];                     //create output variable
            int linearIndex = 0;                                                //create linear index

            for (int i = 0; i < _rosN.Length; i++)                               //for each point in rosN
            {
                int point = _rosN[i];                                            //save the current point linear index for use later
                for (int j = 0; j < 8; j++)                                     //for each neighbor 
                {
                    //weighting is the average of the inverses of the ROS of the neighboring points. If we are for example examining a point and its north neighbor,
                    //we are averaging the inverses of the ROS directions towards the north (not the south, since we are creating an inverse weight matrix technically)
                    if (_rosTheta[point, j] != 0 && _rosTheta[_rosloc[point, j], j] != 0)    //if the point and its neighbor are active
                    {
                        //if the point is N S E or W
                        if (j % 2 == 0)
                        {
                            //calculate weight
                            weight[point, j] = (_cell / 2) * ((1 / _rosTheta[point, (j + 4) % 8]) + (1 / _rosTheta[_rosloc[point, j], (j + 4) % 8]));
                        }
                        //if the point is a corner node (have to account for a longer distance)
                        else
                        {
                            //calculate weight
                            weight[point, j] = sqrt2 * (_cell / 2) * ((1 / _rosTheta[point, (j + 4) % 8]) + (1 / _rosTheta[_rosloc[point, j], (j + 4) % 8]));
                        }
                        //advance linear index
                        linearIndex++;
                    }
                }
            }

            _graph = weight;
        }

        /// <summary>
        /// Create the boundary of the PERIL area
        /// </summary>
        /// <param name="weightList"></param>
        /// <returns></returns>
        public int[] GetBoundary(float[,] weightList)
        {
            //create the new boundary list, includes all nodes within the PERIL boundary
            List<int> boundary = new List<int>();

            //for all the elements on the weight/distance list (output of pathfinder)
            for (int i = 0; i < weightList.GetLength(1); i++)
            {
                for (int j = 0; j < weightList.GetLength(0); j++)
                {
                    //if the weight of the node is less than Tbuffer
                    if (weightList[j, i] <= this._triggerBuffer && weightList[j, i] > 0)
                    {
                        //include it in the boundary list
                        boundary.Add(j);
                    }
                }
            }

            //return all nodes within the boundary
            return boundary.Distinct().ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="boundary"></param>
        /// <returns></returns>
        public int[] CompoundBoundary(int[] boundary)
        {
            //create a new list 
            List<int> uniqueBoundary = new List<int>();
            //Add all the nodes in the boundary in it 
            uniqueBoundary.AddRange(boundary);

            //create a new list to include all nodes NOT on the edge of the boundary (made like this for debugging purposes)
            List<int> lineBoundary = new List<int>();

            for (int i = 0; i < uniqueBoundary.Count; i++)
            {
                if (uniqueBoundary.Contains(uniqueBoundary.ElementAt(i) + 1) && uniqueBoundary.Contains(uniqueBoundary.ElementAt(i) - 1) && uniqueBoundary.Contains(uniqueBoundary.ElementAt(i) - _totalY) && uniqueBoundary.Contains(uniqueBoundary.ElementAt(i) + _totalY))
                {
                    //if all neighbors of the target node are in the boundary matrix
                    lineBoundary.Add(uniqueBoundary.ElementAt(i));  //Add the node to the new List
                }
            }

            //return the difference between the old and new boundary list, should only include the edge nodes
            return uniqueBoundary.Except(lineBoundary).ToArray();
        }

        /// <summary>
        /// Check if the WUI area is not on an active node
        /// </summary>
        /// <param name="wuIx"></param>
        /// <param name="wuIy"></param>
        /// <returns></returns>
        private int[] CheckFireOutOfBounds(int wuIx, int wuIy)
        {
            //create a vector for the WUI point
            int[] newWui = { wuIx, wuIy };

            if (_ros == null)
            {
                Console.WriteLine("FIRE OOB CHECK CURRENTLY DOES NOT WORK WITH ROSCARDINAL INPUTS, INPUT WUI IS RETURNED");
                return newWui;
            }

            //if the WUI is on an inactive note
            if (_ros[wuIy, wuIx] == -9999)
            {
                //show error message
                Console.Write("WARNING: WUI POINT OUT OF FIRE BOUNDS, SUBSTITUTING FOR CLOSEST ACTIVE NODE. ");

                //create new minimum distance variable, set it to infinite.
                float minDistance = int.MaxValue;
                //create new calculated distance variable, set it to infinite.
                float tryout = int.MaxValue;

                for (int i = 0; i < _ros.GetLength(0); i++)          //for all elements in the raster
                {
                    for (int j = 0; j < _ros.GetLength(1); j++)
                    {
                        if (_ros[i, j] != -9999)                    //if the current node is active
                        {
                            tryout = (float)Math.Sqrt(Math.Pow(Math.Abs(i - wuIy), 2) + Math.Pow(Math.Abs(j - wuIx), 2));       //calculate new distance
                            if (minDistance > tryout)       //if the min distance is larger than the new distance
                            {
                                minDistance = tryout;       //set distance as new distance
                                newWui[0] = j;              //Set new WUI point X and Y
                                newWui[1] = i;
                            }
                        }
                    }
                }

                Console.WriteLine("NEW NODE: X = " + newWui[0] + " ,Y = " + newWui[1]);
            }

            //return new WUI area. If WUI was originally on an active node then no change occurs and it returns the original WUI point
            return newWui;
        }

        /// <summary>
        /// Check if the WUI point is outside of Bounds
        /// </summary>
        /// <param name="wuIx"></param>
        /// <param name="wuIy"></param>
        /// <exception cref="Exception"></exception>
        private void CheckGeneralOutOfBounds(int wuIx, int wuIy)
        {
            if (wuIx > _totalX || wuIy > _totalY || wuIx < 0 || wuIy < 0)    //if the point is either outside the max raster size or negative
            {
                Console.WriteLine("MAX X: " + _totalX + " MAX Y: " + _totalY);    //show a console error
                throw new Exception("ERROR: ONE OR MORE COORDINATES OUT OF BOUNDS");                        //throw new exception
            }
        }
        public int[] Linearise(int[,] wui)          //Method to turn the 2D Matrix points to their 1D linearised form
        {
            int[] output = new int[wui.GetLength(0)];       //create new output variable
            for (int i = 0; i < wui.GetLength(0); i++)      //for all the points in the input variable
            {
                output[i] = wui[i, 0] * _totalY + wui[i, 1];     //convert the output from a 2D raster to the 1D network
            }
            return output;
        }

        //Method to turn the raster from a 1D linear naming form to a 2D matrix form
        public int[,] Delinearise(int[] wui)
        {
            int[,] output = new int[wui.Length, 2];         //create new output variable
            for (int i = 0; i < wui.Length; i++)            //for all the elements in the input variable
            {
                output[i, 0] = (int)wui[i] / _totalY;            //convert the output from a linearised network back to the 2D raster
                output[i, 1] = wui[i] % _totalY;
            }
            return output;
        }

        /// <summary>
        /// DFS algorithm. Recursive with stop condition
        /// </summary>
        /// <param name="wui"></param>
        /// <returns></returns>
        private float[] Dfs(int wui)
        {
            //keep track of visited nodes
            bool[] visited = new bool[_graph.GetLength(0)];
            //keep track of nodes to be visited nest
            Queue<int> upNext = new Queue<int>();
            //keep track of distance from node to target
            float[] distance = new float[_graph.GetLength(0)];
            //Current node being measured
            int currentNode = 0;

            //set all points as unvisited, and set distance from all points to wui as infinite
            for (int i = 0; i < _graph.GetLength(0); i++)
            {
                visited[i] = false;
                distance[i] = int.MaxValue;
            }

            //add WUI node as first up next node
            upNext.Enqueue(wui);
            //set distance from WUI to WUI as zero
            distance[wui] = 1;

            //while there are still nodes to be visited
            while (upNext.Count() != 0)
            {
                //set current node
                currentNode = upNext.Peek();
                //for all 8 neighbors of each current node
                for (int i = 0; i < 8; i++)
                {
                    //Try the following (to avoid errors when the neig'hbor of the currrent node is in the edge of the analysis raster
                    if (IsOnBoundary(currentNode) == false)
                    {
                        //if the neighbor of the currrent node is not visited AND the distance from the neighbor node to the WUI is larger than the distance from current node to WUI plus distance from current node to neighbor node
                        if (_rosloc[currentNode, i] != 0 && distance[currentNode] + _graph[currentNode, i] < distance[_rosloc[currentNode, i]] && _graph[currentNode, i] > 0)
                        {
                            distance[_rosloc[currentNode, i]] = distance[currentNode] + _graph[currentNode, i];    //update minimum distance
                            //if tBuffer has not yet been reached AND neighbor node is not inactive
                            if (!visited[_rosloc[currentNode, i]] && distance[_rosloc[currentNode, i]] <= this._triggerBuffer && !(upNext.Contains(_rosloc[currentNode, i])) && _ros[_rosloc[currentNode, i] % _ros.GetLength(0), (int)(_rosloc[currentNode, i] / _ros.GetLength(0))] != -9999)
                            {
                                //add neighbor node to up next list
                                upNext.Enqueue(_rosloc[currentNode, i]);
                            }
                        }
                    }
                }
                //set current node as visited
                visited[currentNode] = true;
                //remove current node from up next list
                upNext.Dequeue();
            }

            //return the distance array
            return distance;
        }

        private bool IsOnBoundary(int node)
        {
            int[] input = { node };
            int[,] coords = Delinearise(input);

            if (coords[0, 0] <= 1 || coords[0, 1] <= 1)
            {
                return true;
            }

            if (coords[0, 0] >= _totalX - 2 || coords[0, 1] >= _totalY - 2)
            {
                return true;
            }
            return false;
        }

        private int[,] GetSafetyMatrix(int[] wuInput)
        {
            //create distance matrix
            float[,] allDistances = new float[_graph.GetLength(0), wuInput.Length];

            //for all WUI nodes
            for (int i = 0; i < wuInput.Length; i++)
            {
                Console.Write("\r Generating Boundary: WUI Nodes Complete: {0}%", i * 100 / wuInput.Length);

                //pathfind from the target WUI node and save resulting array to temp 
                float[] temp = Dfs(wuInput[i]);
                //for all the elements in the temp array
                for (int j = 0; j < _graph.GetLength(0); j++)
                {
                    //parse the array elements to the big output matrix
                    allDistances[j, i] = temp[j];
                }
                DebugMatrix = temp;
            }

            int[,] safetyMatrix = new int[_totalX, _totalY];
            //Get and save the boundary area
            int[] currentDangerZone = GetBoundary(allDistances);

            //Add the boundary area results to the EVAX index matrix
            for (int i = 0; i < currentDangerZone.Length; i++)
            {
                safetyMatrix[currentDangerZone[i] / _totalY, (currentDangerZone[i] % _totalY)]++;
            }

            Console.WriteLine();
            return safetyMatrix;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="wui"></param>
        /// <returns></returns>
        public int[] CheckOutOfBounds(int[,] wui)
        {
            //create the WUI variable (a new one to parse any kind of edit to it)
            int[] wuInput = new int[wui.GetLength(0)];

            //create a temporary Out of bounds vector 
            int[] ooBfixer = new int[2];

            //for all WUI points 
            for (int i = 0; i < wui.GetLength(0); i++)
            {
                //check for out of bounds
                CheckGeneralOutOfBounds(wui[i, 0], wui[i, 1]);
                //check whether WUI is on actice node
                ooBfixer = CheckFireOutOfBounds(wui[i, 0], wui[i, 1]);
                //Linearise WUI accordingly
                wuInput[i] = (ooBfixer[0] - 1) * _totalY + ooBfixer[1];
            }

            return wuInput;
        }
    }   
}