/* 

<><><><><><><><><><><><><><><><><><><><><><><><><><><><><><><><>
     k-PERIL (Population Evacuation Trigger Algorithm)
<><><><><><><><><><><><><><><><><><><><><><><><><><><><><><><><>

================================================================
CODE WRITTEN AND EDITED BY NIKOLAOS KALOGEROPOULOS
ORIGINALLY CREATED BY HARRY MITCHELL
================================================================

----------------------------------------------------------------
DEPARTMENT OF MECHANICAL ENGINEERING
IMPERIAL COLLEGE LONDON
----------------------------------------------------------------

-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

~~~~~~~~~~~~~~~~ WHAT IT DOES ~~~~~~~~~~~~~~~~

THIS CODE AIMS TO WORK WITH FIRE SIMULATION SOFTWARE, SPECIFICALLY TARGETED TO WILDLIFE-URBAN INTERFACE (WUI) AREAS.

IT CREATES A TRIGGER BUFFER REGION AROUND POPULATED AREAS. IF A FIRE CROSSES THE BOUNDARY OF THAT AREA, AN EVACUATION ORDER SHOULD BE ISSUED. 
THESE AREAS HAVE BEEN CREATED TO ENSURE, THAT IF A FIRE REACHED ITS BOUNDARY, AND AN EVACUATION ORDER IS ISSUED, THE PEOPLE IN THE ARE WILL HAVE
ENOUGH TIME TO SAFELY EVACUATE. THIS TRIGGER BUFFER TIME DEPENDS ON MANY VARIABLES AND IS USUALLY AN OUTPUT OF OTHER EVACUATION MODELS.

MORE DETAILS CAN BE FOUND IN https://doi.org/10.1016/j.firesaf.2023.103854. 

THIS SECTION DOES NOT NEED TO BE MODIFIED. IT IS MADE OPEN SOURCE FOR PEOPLE TO SEE HOW IT WORKS. 

~~~~~~~~~~~~~~~~ HOW IT DOES IT ~~~~~~~~~~~~~~~~

PERIL STARTS BY IMPORTING A RATE OF SPREAD MATRIX SET:

>RATEOFSPREAD: MAGNITUDE OF MAXIMUM RATE OF SPREAD
>AZIMUTH : MAJOR PROPAGATION BEARING (DEGREES) FROM VERTICAL. 90 DEGREES MEANS THE FIRE IS SPREADING FROM LEFT TO RIGHT ON THE PLANE (THIS MAY CHANGE DEPENDING ON MODEL USED, SEE DOCUMENTATION OF EACH MODEL)

AS OF NOVEMBER 2021, IT CAN ALSO JUST IMPORT THE RATE OF SPREAD DATA FOR EVERY 8-NEIGHBOR DIRECTION OF A CELL, INSTEAD OF NEEDING TO ESTIMATE THEM BY USING THE WIND (MADE FOR USE IN WUINITY).

FROM THERE K-PERIL INTERPOLATES THE A,B,C VALUES AS PER THE HUYGENS METHOD, AND CALCULATES THE PROPAGATION "WEIGHTS" FROM THE CENTER OF EACH CELL AND INWARDS, ON THE 8 CARDINAL ORTHOGONAL DIRECTIONS.
THAT WAY A WEIGHTING NETWORK IS CREATED. A BFS ALGORITHM IS THEN USED TO FIND THE DISTANCE FROM THE DECLARED WUI-NODES TO ALL OTHER ACTIVE NODES 
(ACTIVE MEANING ON FIRE AT SOME POINT IN THE SIMULATION) UNTIL THE BUFFER TIME IS REACHED. THE NODES IMMEDIATELY ON THE SPECIFIED DISTANCE (HERE EVACUATION TIME, WRSET, TRIGGER 
BUFFER ARE ALL REFERRED TO AS DISTANCE) ARE SAVED AS THE BOUNDARY NODES. 

K-PERIL HAS BEEN MADE SO THAT IT RUNS ONCE FOR EVERY CASE.

~~~~~~~~~~~~~~~~ HOW TO USE IT ~~~~~~~~~~~~~~~~

THIS CODE CAN BE USED FROM SOURCE, AS DONE IN PROGRAM.CS, OR CAN BE USED AS A DLL FILE. 

~~~~~~~~~~~~~~~~ VERSION HISTORY ~~~~~~~~~~~~~~~~

V0.0: PROGRAM CREATED (11/2020)
V0.1: ADDED ROSCARDINALDIRECTIONS (12/2020)
V0.2: ADDED SETWEIGHTNODES (12/2020)
V0.3: ADDED SETROSN (12/2020)
V0.4: ADDED SETROSLOC (12/2020)
V0.5: ADDED SETWEIGHT (12/2020)
V0.6: REMOVED THE WEIGHTNODES MATRIX AND METHOD (12/2020)
V0.7: SHAMELESSLY STOLE A DIJKSTRA ALGO CODE SNIPPET (12/2020)
V0.8: INTEGRATED CODE WITH DIJKSTRA ALGO(12/2020)

V1.0: INITIAL USABLE MODEL: CAN TAKE INPUT FILES AND OUTPUT THE RESULTS IN NEW FILES(12/2020)
V1.1: CHANGED THE PATHFINDING ALGORITHM TO CUSTOM DFS(01/2021)
V1.2: ADDED FUNCTIONALITY TO WORK WHEN WUI IS IN INACTIVE NODE AREA(01/2021)
V1.3: ADDED WUI NODE OUT OUF BOUNDS WARNING (01/2021)
V1.4: ADDED WUI AREA FUNCTIONALITY, SOLVING FOR WUI AREA ABOUNDARY INSTEAD OF NODES
V1.5: ADDED MULTIPLE SIMULATION, COMPOUND BOUNDARY OUTPUT. CHANGED THE INPUT FILE NAMING INPUT
V1.6: ADDED DLL EXTERNAL REFERENCES
V1.7: ADDED EVAX FUNCTIONALITY - K-PERIL NOW INCLUDES METHODS TO ADD INDIVIDUAL BOUNDARIES TOGETHER.
V1.8: CHANGED FLOW OF DATA FROM TEXT FILE BASED TO MEMORY BASED
V1.9: SPLIT THE MAIN PERIL METHODS TO THREE (GET SAFE AREA, GET OVERALL BOUNDARY, GET EVAX INDEX)
V1.10: MODIFIED THE MAIN PERIL METHODS TO ACCOMODATE MONTE CARLO (MEMORY INTENSIVE) SIMULATIONS. DELETED OLD METHODS
V1.11: CHANGED THE STRUCTURE OF THE ALGORITHM TO MORE EFFICIENTLY AND CORRECTLY USE CLASSES AND INTERNAL VARIABLES ETC. ALSO LEARNED THAT TWO METHODS WITH DIFFERENT CONSTRUCTORS WORKS SO MERGED SOME METHODS TOGETHER (GETSINGULARBOUNDARY)

 */

using NetTopologySuite.Algorithm;
using NetTopologySuite.Operation.Distance;
using RoxCaseGen;
using System;
using System.Collections.Generic;
using System.Linq;

namespace k_PERIL_DLL
{
    class PERILcore       //peril setup methods
    {
        private float[,] azimuth;                 //Rate of spread magnitude
        private float[,] ROS;                   //Rate of Spread magnitude (coupled with azimuth)
        private float[,] ROStheta;              //rate of spread split onto the eight cardinal directions
        private float U;                        //Mid-flame wind speed
        private int totalY;                     //Total raster size in Y
        private int totalX;                     //Total raster size in X
        private int[] rosn;                     //linearised array of all the non-boundary nodes
        private float cell;                       //cell size (m)
        private int tBuffer;                    //trigger buffer time in minutes
        private int[,] rosloc;                  //tracks the cardinal neighbors of each linearised cell.
        private float[,] graph;                 //variable to store the weight values for the target node

        public float[] debugMatrix;             //debug purposes

        public void setValues(float cell, int tBuffer, float U, float[,] ROS, float[,] azimuth, int totalX, int totalY)
        {
            this.cell = cell;
            this.tBuffer = tBuffer;
            this.U = (float)(U / 1.15);             //Windspeed to mid flame windspeed correction factor
            this.ROS = ROS;
            this.azimuth = azimuth;
            this.totalX = totalX;
            this.totalY = totalY;
        }
        public void SetROStheta() //calculate ros, includes rate of spread to all 8 cardinal directions
        {
            double LB = 0.936 * Math.Exp(0.2566 * U) + 0.461 * Math.Exp(-0.1548 * U) - 0.397; //Calculate length to Breadth ratio of Huygens ellipse
            double HB = (LB + (float)Math.Sqrt(LB * LB - 1)) / (LB - (float)Math.Sqrt(LB * LB - 1));    //calculate head to back ratio

            int totalY = azimuth.GetLength(0);   //get maximum y dimension
            int totalX = azimuth.GetLength(1);   //get maximum x dimension

            float a = new float();              //create variable a for huygens ellipse
            float b = new float();              //create variable b for huygens ellipse
            float c = new float();              //create variable c for huygens ellipse
            double ROSX = new double();         //create variable ROSX for ROS calculation
            double ROSY = new double();         //create variable ROSY for ROS calculation
            float[,] ROStheta = new float[totalX * totalY, 8];  //create output matrix variable

            int linearIndex = 0; //create linearisation index variable

            //the code converts the raster from an X x Y raster to a X*Y x 1 linear array of elements. This makes further calculations easier as the resulting network of nodes is just a list of linear points.

            for (int x = 0; x < totalX; x++)    //for every element in the raster
            {
                for (int y = 0; y < totalY; y++)
                {
                    a = (ROS[y, x] / (2 * (float)LB)) * (1 + 1 / (float)HB);       //Calculate a
                    b = (ROS[y, x] / 2) * (1 + 1 / (float)HB);                     //Calculate b
                    c = (ROS[y, x] / 2) * (1 - 1 / (float)HB);                     //Calculate c

                    for (int cardinal = 0; cardinal < 8; cardinal++)                //for every cardinal direction (starting from north and going clockwise)
                    {
                        if (azimuth[y, x] != -9999)                                 //if the cell is active i.e. has been burned in the simulation
                        {
                            ROSX = a * Math.Sin((Math.PI * cardinal / 4) - azimuth[y, x] * 2 * Math.PI / 360);              //Calculate ROSX
                            ROSY = c + b * Math.Cos((Math.PI * cardinal / 4) - azimuth[y, x] * 2 * Math.PI / 360);          //Calculate ROSY
                            ROStheta[linearIndex, cardinal] = (float)Math.Sqrt(Math.Pow(ROSX, 2) + Math.Pow(ROSY, 2));      //Calculate ROS per cardinal direction
                        }
                        else { ROStheta[linearIndex, cardinal] = 0; }    //if the cell is inactive, set ros to zero
                    }
                    linearIndex++;                                                  //advance the linear index
                }
            }
            this.ROStheta = ROStheta;                                                        //return completed matrix
        }

        public void SetROStheta(float[,] rosTheta)
        {
            this.ROStheta = rosTheta;
        }

        public void SetRosN() //calculate rosn, a list of all the non-boundary nodes (boundary nodes do not have 8 neighbors and complicate the rest of the algorithm. As such only internal nodes are used, and boundary nodes are only used as "neighbors" for calculations
        {
            List<int> rosN = new List<int>();               //create a new list for rosN

            for (int i = 1; i < totalX-1; i++)              //for the X and Y dimensions
            {
                for (int j = 1; j < totalY-1; j++)
                {
                    rosN.Add(i * totalY + j);               //add it to the list                        
                }
            }
            this.rosn=rosN.ToArray();                          //return rosN as an array
        }
        public void SetRosLoc() //calculate rosloc, a catalog of the neighbors of each node. Orientation is same as ROS cardinal direction, starts from North and moves clockwise.
        {
            int[,] Rosloc = new int[rosn.Max() + 1, 8];           //create output variable

            for (int i = 0; i < rosn.Length; i++)               //for every element is rosn calculate and catalog its linearised neighbor
            {
                Rosloc[rosn[i], 0] = rosn[i] - 1;               //North
                Rosloc[rosn[i], 1] = rosn[i] + totalY - 1;      //NE
                Rosloc[rosn[i], 2] = rosn[i] + totalY;          //east
                Rosloc[rosn[i], 3] = rosn[i] + totalY + 1;      //SE
                Rosloc[rosn[i], 4] = rosn[i] + 1;               //South
                Rosloc[rosn[i], 5] = rosn[i] - totalY + 1;      //SW
                Rosloc[rosn[i], 6] = rosn[i] - totalY;          //west
                Rosloc[rosn[i], 7] = rosn[i] - totalY - 1;      //NW          
            }
            this.rosloc = Rosloc;
        }
        public void SetWeight() //calculate the weight variable. contains the "Weight" between each node and its 8 neighbors
        {
            float[,] weight = new float[rosn.Max() + 1, 8];                     //create output variable
            int linearIndex = 0;                                                //create linear index

            for (int i = 0; i < rosn.Length; i++)                               //for each point in rosN
            {
                int point = rosn[i];                                            //save the current point linear index for use later
                for (int j = 0; j < 8; j++)                                     //for each neighbor 
                {
                    //weighting is the average of the inverses of the ROS of the neighboring points. If we are for example examining a point and its north neighbor, we are averaging the inverses of the ROS directions towards the north (not the south, since we are creating an inverse weight matrix technically)
                    if (ROStheta[point, j] != 0 && ROStheta[rosloc[point, j], j] != 0)    //if the point and its neighbor are active
                    {
                        if (j % 2 == 0)                                         //if the point is N S E or W
                        {
                            weight[point, j] = (cell / 2) * ((1 / ROStheta[point, (j + 4) % 8]) + (1 / ROStheta[rosloc[point, j], (j + 4) % 8]));                         //calculate weight
                        }
                        else                                                    //if the point is a corner node (have to account for a longer distance)
                        {
                            weight[point, j] = (float)Math.Sqrt(2) * (cell / 2) * ((1 / ROStheta[point, (j + 4) % 8]) + (1 / ROStheta[rosloc[point, j], (j + 4) % 8]));   //calculate weight
                        }
                        linearIndex++;                                          //advance linear index
                    }
                }
            }
            this.graph = weight;
        }
        public int[] getBoundary(float[,] weightList)      //create the boundary of the PERIL area
        {
            List<int> boundary = new List<int>();                               //create the new boundary list, includes all nodes within the PERIL boundary

            for (int i = 0; i < weightList.GetLength(1); i++)                   //for all the elements on the weight/distance list (output of pathfinder)
            {
                for (int j = 0; j < weightList.GetLength(0); j++)
                {
                    if (weightList[j, i] <= this.tBuffer && weightList[j, i] > 0)                       //if the weight of the node is less than Tbuffer
                    {
                        boundary.Add(j);                                        //include it in the boundary list
                    }
                }
            }
            return boundary.Distinct().ToArray();                                          //return all nodes within the boundary
        }
        public int[] compoundBoundary(int[] boundary)
        {
            List<int> uniqueBoundary = new List<int>();                         //create a new list 
            uniqueBoundary.AddRange(boundary);                                  //Add all the nodes in the boundary in it 

            List<int> lineBoundary = new List<int>();                           //create a new list to include all nodes NOT on the edge of the boundary (made like this for debugging purposes)

            for (int i = 0; i < uniqueBoundary.Count; i++)
            {
                if (uniqueBoundary.Contains(uniqueBoundary.ElementAt(i) + 1) && uniqueBoundary.Contains(uniqueBoundary.ElementAt(i) - 1) && uniqueBoundary.Contains(uniqueBoundary.ElementAt(i) - totalY) && uniqueBoundary.Contains(uniqueBoundary.ElementAt(i) + totalY))
                {
                    //if all neighbors of the target node are in the boundary matrix
                    lineBoundary.Add(uniqueBoundary.ElementAt(i));  //Add the node to the new List
                }
            }
            return uniqueBoundary.Except(lineBoundary).ToArray();   //return the difference between the old and new boundary list, should only include the edge nodes
        }
        public int[] checkFireOOB(int WUIx, int WUIy)           //check if the WUI area is not on an active node 
        {
            int[] newWUI = { WUIx, WUIy };                              //create a vector for the WUI point

            if (ROS == null)
            {
                Console.WriteLine("FIRE OOB CHECK CURRENTLY DOES NOT WORK WITH ROSCARDINAL INPUTS, INPUT WUI IS RETURNED");
                return newWUI;
            }
            if (ROS[WUIy, WUIx] == -9999)                           //if the WUI is on an inactive note
            {   //show error message
                Console.Write("WARNING: WUI POINT OUT OF FIRE BOUNDS, SUBSTITUTING FOR CLOSEST ACTIVE NODE. ");

                float minDistance = int.MaxValue;                       //create new minimum distance variable, set it to infinite. 
                float tryout = int.MaxValue;                            //create new calculated distance variable, set it to infinite. 

                for (int i = 0; i < ROS.GetLength(0); i++)          //for all elements in the raster
                {
                    for (int j = 0; j < ROS.GetLength(1); j++)
                    {
                        if (ROS[i, j] != -9999)                    //if the current node is active
                        {
                            tryout = (float)Math.Sqrt(Math.Pow(Math.Abs(i - WUIy), 2) + Math.Pow(Math.Abs(j - WUIx), 2));       //calculate new distance
                            if (minDistance > tryout)       //if the min distance is larger than the new distance
                            {
                                minDistance = tryout;       //set distance as new distance
                                newWUI[0] = j;              //Set new WUI point X and Y
                                newWUI[1] = i;
                            }
                        }
                    }
                }
                Console.WriteLine("NEW NODE: X = " + newWUI[0] + " ,Y = " + newWUI[1]);    //output it on console
            }
            return newWUI;  //return new WUI area. If WUI was originally on an active node then no change occurs and it returns the original WUI point
        }
        public void checkGeneralOOB(int WUIx, int WUIy)         //check if the WUI point is outside of Bounds
        {
            if (WUIx > totalX || WUIy > totalY || WUIx < 0 || WUIy < 0)    //if the point is either outside the max raster size or negative
            {
                Console.WriteLine("MAX X: " + totalX + " MAX Y: " + totalY);    //show a console error
                throw new Exception("ERROR: ONE OR MORE COORDINATES OUT OF BOUNDS");                        //throw new exception
            }
        }
        public int[] linearise(int[,] WUI)          //Method to turn the 2D Matrix points to their 1D linearised form
        {
            int[] output = new int[WUI.GetLength(0)];       //create new output variable
            for (int i = 0; i < WUI.GetLength(0); i++)      //for all the points in the input variable
            {
                output[i] = WUI[i, 0] * totalY + WUI[i, 1];     //convert the output from a 2D raster to the 1D network
            }
            return output;
        }
        public int[,] delinearise(int[] WUI)        //Method to turn the raster from a 1D linear naming form to a 2D matrix form
        {
            int[,] output = new int[WUI.Length, 2];         //create new output variable
            for (int i = 0; i < WUI.Length; i++)            //for all the elements in the input variable
            {
                output[i, 0] = (int)WUI[i] / totalY;            //convert the output from a linearised network back to the 2D raster
                output[i, 1] = WUI[i] % totalY;
            }
            return output;
        }
        public float[] dfs(int WUI)//DFS algorithm. Recursive with stop condition
        {
            bool[] visited = new bool[graph.GetLength(0)];      //keep track of visited nodes
            Queue<int> upNext = new Queue<int>();           //keep track of nodes to be visited nest
            float[] distance = new float[graph.GetLength(0)];   //keep track of distance from node to target
            int currentNode = 0;                                //Current node being measured

            for (int i = 0; i < graph.GetLength(0); i++)       //set all points as unvisited, and set distance from all points to wui as infinite
            {
                visited[i] = false;
                distance[i] = int.MaxValue;
            }

            upNext.Enqueue(WUI);                                    //add WUI node as first up next node
            distance[WUI] = 1;                                   //set distance from WUI to WUI as zero
            while (upNext.Count() != 0)                         //while there are still nodes to be visited
            {
                currentNode = upNext.Peek();              //set current node
                for (int i = 0; i < 8; i++)                     //for all 8 neighbors of each current node
                {
                    if (isOnBoundary(currentNode)==false)                                       //Try the following (to avoid errors when the neig'hbor of the currrent node is in the edge of the analysis raster
                    {
                        if (rosloc[currentNode, i]!=0 && distance[currentNode] + graph[currentNode, i] < distance[rosloc[currentNode, i]] && graph[currentNode, i] > 0) //if the neighbor of the currrent node is not visited AND the distance from the neighbor node to the WUI is larger than the distance from current node to WUI plus distance from current node to neighbor node
                        {
                            distance[rosloc[currentNode, i]] = distance[currentNode] + graph[currentNode, i];    //update minimum distance
                            if (!visited[rosloc[currentNode, i]]  && distance[rosloc[currentNode, i]] <= this.tBuffer && !(upNext.Contains(rosloc[currentNode, i])) && ROS[rosloc[currentNode, i] % ROS.GetLength(0), (int)(rosloc[currentNode, i] / ROS.GetLength(0))] != -9999) //if tBuffer has not yet been reached AND neighbor node is not inactive
                            {
                                upNext.Enqueue(rosloc[currentNode, i]);     //add neighbor node to up next list
                            }
                        }
                    }
                }
                visited[currentNode] = true;                    //set current node as visited
                upNext.Dequeue();                     //remove current node from up next list
            }
            /*
            int[,] output = new int[this.totalY, this.totalX];
            for (int i = 0; i < this.totalY - 1; i++)            //for all the elements in the input variable
            {
                for (int j = 0; j < this.totalX - 1; j++)
                {
                    output[i, j] = (int)this.graph[j * totalY + i,2]*1000;            //convert the output from a linearised network back to the 2D raster
                }
            }
            FlammapSetup.OutputFile(output, @"C:\Users\nikos\source\repos\RoxCaseGen\Outputs\individualTimes.txt");
            */
            return distance;                                    //return the distance array
        }

        private bool isOnBoundary(int node)
        {
            int[] input = { node };
            int[,] coords = delinearise(input);
            if (coords[0,0]<=1 || coords[0, 1] <= 1)
            {
                return true;
            }
            if (coords[0, 0] >= this.totalX - 2 || coords[0, 1] >= this.totalY - 2)
            {
                return true;
            }
            return false;
        }
        public int[,] getSafetyMatrix(int[] WUInput)
        {
            float[,] allDistances = new float[graph.GetLength(0), WUInput.Length];   //create distance matrix

            for (int i = 0; i < WUInput.Length; i++)                                        //for all WUI nodes
            {
                Console.Write("\r Generating Boundary: WUI Nodes Complete: {0}%",i*100/WUInput.Length);
                float[] temp = dfs(WUInput[i]);                                               //pathfind from the target WUI node and save resulting array to temp 
                for (int j = 0; j < graph.GetLength(0); j++)                         //for all the elements in the temp array
                {
                    allDistances[j, i] = temp[j];                                           //parse the array elements to the big output matrix
                }
                this.debugMatrix = temp;
            }
            /*
            int[,] output =new int[this.totalY,this.totalX];
            for (int i = 0; i<this.totalY-1; i++)            //for all the elements in the input variable
            {
                for (int j = 0; j<this.totalX-1; j++)
                {
                    output[i,j] = (int)this.debugMatrix[j*totalY + i];            //convert the output from a linearised network back to the 2D raster
                }
            }
            FlammapSetup.OutputFile(output, @"C:\Users\nikos\source\repos\RoxCaseGen\Outputs\arrivalTime.txt");
            */
            

            int[,] safetyMatrix = new int[totalX,totalY];
            int[] currentDangerZone = getBoundary(allDistances);                    //Get and save the boundary area
            
            for (int i = 0; i < currentDangerZone.Length; i++)                              //Add the boundary area results to the EVAX index matrix
            {
                safetyMatrix[currentDangerZone[i] / totalY, (currentDangerZone[i] % totalY)]++;
            }
            Console.WriteLine();
            return safetyMatrix;
        }
        public int[] checkOOB(int[,] WUI)
        {
            int[] WUInput = new int[WUI.GetLength(0)];                                      //create the WUI variable (a new one to parse any kind of edit to it)
            int[] OOBfixer = new int[2];                                                    //create a temporary Out of bounds vector 

            for (int i = 0; i < WUI.GetLength(0); i++)                                      //for all WUI points 
            {
                checkGeneralOOB(WUI[i, 0], WUI[i, 1]);                               //check for out of bounds
                OOBfixer = checkFireOOB(WUI[i, 0], WUI[i, 1]);                       //check whether WUI is on actice node
                WUInput[i] = (OOBfixer[0] - 1) * totalY + OOBfixer[1];                        //Linearise WUI accordingly
            }

            return WUInput;
        }
    }
    public class PERIL           //the actual program
    {

        private List<int[,]> allBoundaries = new List<int[,]>();
        int[] rasterSize = new int[2];

        /// <summary>
        /// This method represents one iteration.
        /// </summary>
        /// <param name="cell">The square size of each point (most commonly 30m)</param>
        /// <param name="tBuffer">The prescribed evacuation time, in minutes</param>
        /// <param name="windSpeed">The wind speed in the midflame height, representing the entire domain (spatially and temporally)</param>
        /// <param name="WUIarea">An X by 2 array listing points defining a polygon. This polygon is used as the urban area around which the boundary is calculated.The dimensions of each point are about the domain with (0,0) being the top left corner. </param>
        /// <param name="ROS">The rate of spread magniture array of size X by Y, in meters per minute</param>
        /// <param name="azimuth">The rate of spread direction array of size X by Y, in degrees from north, clockwise</param>
        /// <returns>An X by Y array representing the landscape. Points are 1 if inside the boundary and 0 if outside.</returns>
        public int[,] calcSingularBoundary(float cell, int tBuffer, float windSpeed, int[,] WUInodes, float[,] ROS, float[,] azimuth) //The main function
        {
            PERILcore solver = new PERILcore();                                        //instantiate peril preparation

            int yDim = azimuth.GetLength(0);
            int xDim = azimuth.GetLength(1);

            this.rasterSize[0] = xDim;
            this.rasterSize[1] = yDim;

            int[,] WUIarea = getPolygonEdgeNodes(WUInodes);

            solver.setValues(cell, tBuffer, windSpeed, ROS, azimuth, xDim, yDim);

            int[,] WUI = solver.delinearise(solver.compoundBoundary(solver.linearise(WUIarea)));     //Linearise the WUIarea array, get its boundary, and delinearise
            /*
            Console.Write("WUI Boundary Nodes: ");//Output the WUI area boundary generated in the Console
            for (int i = 0; i < WUI.GetLength(0); i++)
            {
                Console.Write(WUI[i, 0] + "," + WUI[i, 1] + "   ");
            }
            Console.WriteLine();
            */
            int[] WUInput = solver.checkOOB(WUI);

            bool noFire = false;

            for (int i = 0; i < WUInput.Length; i++)
            {
                if (WUInput[i].Equals(int.MaxValue))
                {
                    Console.WriteLine("FIRE DOES NOT SIGNIFICANTLY REACH THE AFFECTED AREA, PERIL WILL NOT TAKE THIS FIRE INTO ACCOUNT");
                    noFire = true;
                }
            }
            
            if (!noFire)
            {

                if (ROS.GetLength(1) == 8)
                {
                    solver.SetROStheta(ROS);
                }
                else
                {
                    solver.SetROStheta(); 
                }
                //Console.WriteLine("ROS_THETA GENERATED");                                       //console confirmation message

                solver.SetRosN();                                                               //Calculate ROSn
                //Console.WriteLine("ROSn GENERATED");                                            //console confirmation message

                solver.SetRosLoc();                                                             //Calculate Rosloc
                //Console.WriteLine("ROSLOC GENERATED");                                          //console confirmation message

                solver.SetWeight();                                                             //Calculate Weights Matrix
                //Console.WriteLine("WEIGHTS GENERATED");                                         //console confirmation message

                int[,] safetyMatrix = solver.getSafetyMatrix(WUInput);
                Console.WriteLine("BOUNDARY GENERATED");                                        //console confirmation message

                return safetyMatrix;
            }
            return null;
        }
        /// <summary>
        /// The main callable method of k-PERIL.This method calculates multiple iterations and saves them within the object.
        /// </summary>
        /// <param name="cell"> An array of The square size of each point (most commonly 30m) for each simulation</param>
        /// <param name="tBuffer"> An array ofThe prescribed evacuation time, in minutes</param>
        /// <param name="windSpeed">An array of The wind speed in the midflame height, representing the entire domain (spatially and temporally)</param>
        /// <param name="WUIarea">A jagged array of X by 2 array listing points defining a polygon. This polygon is used as the urban area around which the boundary is calculated.The dimensions of each point are about the domain with (0,0) being the top left corner. </param>
        /// <param name="ROS">A jagged array of The rate of spread magniture array of size X by Y, in meters per minute</param>
        /// <param name="azimuth">A jagged array of The rate of spread direction array of size X by Y, in degrees from north, clockwise</param>
        /// <returns>An X by Y array representing the landscape. Points are 1 if inside the boundary and 0 if outside.</returns>

        public int[,] calcMultipleBoundaries(float[] cell, int[] tBuffer, float[] windSpeed, int[][,] WUIarea, float[][,] ROS, float[][,] azimuth)
        {
            for (int i=0; i<cell.Length; i++)
            {
                allBoundaries.Append(calcSingularBoundary(cell[i], tBuffer[i], windSpeed[i], WUIarea[i], ROS[i], azimuth[i]));
            }
            return getProbBoundary();
        }
        /// <summary>
        /// Sums up all the boundaries calculated in calcMultipleBoundaries.
        /// </summary>
        /// <returns>An X by Y array representing the domain</returns>
        public int[,] getProbBoundary()
        {
            int[,] output = new int[this.rasterSize[0], this.rasterSize[1]];
            foreach (int[,] boundary in allBoundaries)
            {
                for (int i = 0; i < this.rasterSize[0]; i++)
                {
                    for (int j = 0; j < this.rasterSize[1]; j++)
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
        public List<int[,]> getBoundaryList()
        {
            return allBoundaries;
        }

        public static int[,] getLineBoundary(int[,] compoundBoundary)          //Method to get the boundary line from a safety matrix (unlike getCompoundBoundary which needs multiple safety matrices in one matrix to work)
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

            foreach (int boundaryPoint in uniqueBoundary)                         //For every boundary point, add its X and Y values to the output matrix
            {
                output[count, 0] = (boundaryPoint % compoundBoundary.GetLength(1))+1;   //Added the +1 since the indices in the cartesian system must start from 1
                output[count, 1] = (boundaryPoint / compoundBoundary.GetLength(1))+1;
                count++;
            }

            return output;   //return the new boundary matrix, should only include the edge nodes

        }

        public static int[,] getDenseBoundaryFromProbBoundary(int[,] probBoundary, int noOfLines)
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
                /*
                for (int j = 1; j < probBoundary.GetLength(0)-1; j++)
                {
                    for (int k = 1; k < probBoundary.GetLength(1) - 1; k++)
                    {
                        if (isolatedBoundary[j, k] > 0) 
                        {
                            if (isolatedBoundary[j + 1, k] == 0 || isolatedBoundary[j, k + 1] == 0 || isolatedBoundary[j - 1, k] == 0 || isolatedBoundary[j, k - 1] == 0)
                            {
                                outputBoundary[j, k] = i + 1;
                            }
                        }
                    }
                }
                */
            } 
            return outputBoundary;
        }

        /*
        public static int[,] getSingularBoundary(float cellSize, int tBuffer, int[,] WUIarea, float[,] ROStheta)                 //Method to call the main k-PERIL program with ready ROScardinal inputs
        {
            PERILcore solver = new PERILcore();                                        //instantiate peril preparation

            int yDim = ROStheta.GetLength(0);
            int xDim = ROStheta.GetLength(1);
            
            int[,] WUI = solver.delinearise(solver.compoundBoundary(solver.linearise(WUIarea)));     //Linearise the WUIarea array, get its boundary, and delinearise

            Console.Write("WUI Boundary Nodes: ");//Output the WUI area boundary generated in the Console
            for (int i = 0; i < WUI.GetLength(0); i++)
            {
                Console.Write(WUI[i, 0] + "," + WUI[i, 1] + "   ");
            }
            Console.WriteLine();

            int[] WUInput = solver.checkOOB(WUI);

            solver.SetRosN();                                                               //Calculate ROSn
            //Console.WriteLine("ROSn GENERATED");                                            //console confirmation message

            solver.SetRosLoc();                                                             //Calculate Rosloc
            //Console.WriteLine("ROSLOC GENERATED");                                          //console confirmation message

            solver.SetWeight();                                                             //Calculate Weights Matrix
            //Console.WriteLine("WEIGHTS GENERATED");                                         //console confirmation message

            int[,] safetyMatrix = solver.getSafetyMatrix(WUInput);
            //Console.WriteLine("BOUNDARY GENERATED");                                        //console confirmation message

            return safetyMatrix;
        }
        */

        /// <summary>
        /// Function that finds all points defining the perimeter of a polygon. Used to find all points of the WUI area boundary
        /// </summary>
        /// <param name="endNodes"> Array of X by 2 representing the coordinates of the polygon nodes</param>
        /// <returns>Array of Y by 2 of all the points in the perimeter of the polygon</returns>
        public static int[,] getPolygonEdgeNodes(int[,] endNodes)
        {
            int noNodes = endNodes.GetLength(0);

            List<int[]> allNodes = new List<int[]>();
            for (int i = 0; i < noNodes-1; i++)
            {
                int[,] endPair = new int[,] { { endNodes[i,0], endNodes[i, 1] }, { endNodes[i+1, 0], endNodes[i+1, 1] } };

                int[,] oneEdge = getAllNodesBetween(endPair);

                for (int j = 0; j < oneEdge.GetLength(0); j++)
                {
                    int[] interPoints = new int[2] { oneEdge[j, 0], oneEdge[j, 1] };
                    allNodes.Add(interPoints);
                }
            }
            int[,] finalEndPair = new int[,] { { endNodes[noNodes-1, 0], endNodes[noNodes-1, 1] }, { endNodes[0, 0], endNodes[0, 1] } };

            int[,] finalOneEdge = getAllNodesBetween(finalEndPair);

            for (int j = 0; j < finalOneEdge.GetLength(0); j++)
            {
                int[] interPoints = new int[2] { finalOneEdge[j, 0], finalOneEdge[j, 1] };
                allNodes.Add(interPoints);
            }
            return PERIL.get2DarrayFromIntList(allNodes);
        }

        public static int[,] getAllNodesBetween(int[,] endNodes)
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
                double InverseSlope = (x2 - x1) / (y2 - y1);
                for (int count = 1; count < Math.Abs(y2 - y1) + 1; count++)
                {
                    i = (int)(count * (double)(y2 - y1) / (double)Math.Abs(y2 - y1));
                    double errorTop = Math.Abs(tempX + 1 - (InverseSlope * i + x1));
                    double errorCenter = Math.Abs(tempX - (InverseSlope * i + x1));
                    double errorBottom = Math.Abs(tempX - 1 - (InverseSlope * i + x1));

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
            return PERIL.get2DarrayFromIntList(allNodes);
        }

        public static int[,] get2DarrayFromIntList(List<int[]> list)
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

        internal static int[,] getRasterBoundaryFromProbBoundary(int[,] safetyMatrix, float v)
        {
            throw new NotImplementedException();
        }
    }
}

/* OLD METHODS (THAT MIGHT BE USEFUL STILL)

#####################################################################################
        public int[,] getCompoundBoundary(int[,,] allBoundaries)   //OUTDATED, REPLACED WITH getLineBoundary. This method aims to get the coordinates of the boundary nodes from the array with all the safety matrices 
        {
            List<int> uniqueBoundary = new List<int>();                                     //make a new list that will contain all the nodes whose safetyboundary value is nonzero

            for (int i = 0; i < allBoundaries.GetLength(2); i++)                            //For every point in the compound safety matrix (i.e. every node in every case)
            {
                for (int x = 0; x < allBoundaries.GetLength(0); x++)
                {
                    for (int y = 0; y < allBoundaries.GetLength(1); y++)
                    {
                        if (allBoundaries[x, y, i] == 1)
                        {
                            uniqueBoundary.Add(x * allBoundaries.GetLength(1) + y);         //If the value of the cell is 1 (inside the safety boundary) add it to the list (by linearising its index)
                        }
                    }
                }
            }

            List<int> lineBoundary = new List<int>();                                       //create a new list to include all nodes NOT on the edge of the boundary (made like this for debugging purposes)
            List<int> noDupes = uniqueBoundary.Distinct().ToList();                         //Get rid of duplicate cell indices from the list

            for (int i = 0; i < noDupes.Count; i++)                                         //For every element inside a boundary
            {
                if (!(noDupes.Contains(noDupes.ElementAt(i) + 1) && noDupes.Contains(noDupes.ElementAt(i) - 1) && noDupes.Contains(noDupes.ElementAt(i) - allBoundaries.GetLength(1)) && noDupes.Contains(noDupes.ElementAt(i) + allBoundaries.GetLength(1))))
                {
                    //if NOT all neighbors of the target node are in the boundary matrix / if any of the nodes 4 neighbors is missing from the noDupes list 
                lineBoundary.Add(noDupes.ElementAt(i));                                     //Add the node to the new List
                }
            }

            int[,] output = new int[lineBoundary.Count, 2];                                 //make a new matrix to export the boundary in [X,Y] form
            int count = 0;

            foreach (int boundaryPoint in lineBoundary)                                     //For every boundary point, add its X and Y values to the output matrix
            {
                output[count, 0] = boundaryPoint / allBoundaries.GetLength(1);
                output[count, 1] = boundaryPoint % allBoundaries.GetLength(1);
                count++;
            }

            return output;                                                                  //return the boundary list, should only include the edge nodes
        }

###############################################################

        public int[,] getEVAXmatrix(int[,,] allBoundaries, int[,] WUIarea)      //OUTDATED, NO LONGER USED. Method to add multiple safetymatrices together. 
        {
            int[,] EVAXmatrix = new int[allBoundaries.GetLength(0), allBoundaries.GetLength(1)];                //Create a new matrix, witht hte same size as the safety matrices

            for (int y = 0; y < allBoundaries.GetLength(1); y++)                                                //For every element in the allboundaries compound matrix
            {
                for (int x = 0; x < allBoundaries.GetLength(0); x++)
                {
                    for (int i = 0; i < allBoundaries.GetLength(2); i++)
                    {
                        EVAXmatrix[x,y] += allBoundaries[x, y, i];                                              //Add up- the values of each cell in each safety matrix
                    }
                }
            }

            for (int i = 0; i < WUIarea.GetLength(0); i++)
            {
                EVAXmatrix[WUIarea[i, 0], WUIarea[i, 1]] = 255;                                                 //Mark WUI areas in the EVAX matrix as 255
            }

            return EVAXmatrix;
        }

#############################################################################

    class ParseInputFiles   //get and open the input files
    {
        public float[,] getFile(string Path)                                //save info in a text file as a variable (shamelessly stolen off the internet, read these comments with a hit of suspition)
        {
            var data = System.IO.File.ReadAllText(Path);                    //Save all data in var data
            var arrays = new List<float[]>();                               //create a new list of float arrays
            var lines = data.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);  //split the data var by line and save each line as an array

            foreach (var line in lines)                                     //for each line variable in the list(?) lines
            {
                var lineArray = new List<float>();                          //make a new list of floats
                foreach (var s in line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))   //for each variable s in the input array
                {
                    lineArray.Add(float.Parse(s, System.Globalization.NumberStyles.Float)); //add the element to the list of floats created before the loop
                }
                arrays.Add(lineArray.ToArray());                            //add the lineArray array to the general arrays list
            }
            var numberOfRows = lines.Count();                               //save the number of rows of the parsed and edited data in lines
            var numberOfValues = arrays.Sum(s => s.Length);                 //save the total number of values in arrays (i.e. all elements)

            int minorLength = arrays[0].Length;                             //create a variable to save the minor dimension of the arrays variable
            float[,] NumOut = new float[arrays.Count, minorLength];         //create output matrix
            for (int i = 0; i < arrays.Count; i++)                          //for all elements in arrays
            {
                var array = arrays[i];                                      //save the array in the arrays element
                for (int j = 0; j < minorLength; j++)                       //for the length of the minor array
                {
                    NumOut[i, j] = array[j];                                //place each element in the output matrix
                }
            }
            return NumOut;                                                  //return the output matrix
        }
    }
    
################################################################4

    public void outputEPI(int[] EVAXarray, int yDim, string EVAXoutput, int[,] WUIarea)     //Method to create and output the EVAX matrix
        {
            int[,] output = new int[yDim, EVAXarray.Length / yDim];                             //Create the output matrix

            for (int i = 0; i < EVAXarray.Length; i++)                                          //Delinearise the input array
            {
                output[(int)i / yDim, i % yDim] = EVAXarray[i];
            }

            for (int i = 0; i < WUIarea.GetLength(0); i++)                                      //Set the WUI nodes as 9999 in the matrix
            {
                output[WUIarea[i, 0], WUIarea[i, 1]] = 0;
            }

            using (var sw = new StreamWriter(EVAXoutput))  //beyond here the code has been shamelessly stolen
            {
                for (int i = 0; i < output.GetLength(0); i++)   //for all elements in the output array
                {
                    for (int j = 0; j < yDim; j++)
                    {
                        sw.Write(output[i, j] + " ");       //write the element in the file
                    }
                    sw.Write("\n");                         //enter new line
                }
                sw.Flush();                                 //i dont really know
                sw.Close();                                 //close opened output text file
            }
        }
    
#############################################################

        public void outputFile(int[] boundary, int yy, string PerilOutput, int yDim)      //output variable to a new text file
        {
            int[,] output = delinearise(boundary, yy);

            using (var sw = new StreamWriter(PerilOutput))  //beyond here the code has been shamelessly stolen
            {
                for (int i = 0; i < boundary.Length; i++)   //for all elements in the output array
                {
                    for (int j = 0; j < 2; j++)
                    {
                        sw.Write(output[i, j] + " ");       //write the element in the file
                    }
                    sw.Write("\n");                         //enter new line
                }
                sw.Flush();                                 //i dont really know
                sw.Close();                                 //close opened output text file
            }
        }

#########################################################################

        public void DEBUGoutputFile(float[,] boundary, int yy, string PerilOutput)      //output variable to a new text file
        {
            float[,] output = new float[boundary.GetLength(0), 3];    //create new output variable
            for (int i = 0; i < boundary.GetLength(0); i++)       //for all the elements in the input variable
            {
                output[i, 0] = i / yy;       //convert the output from a linearised network back to the 2D raster
                output[i, 1] = i % yy;
                output[i, 2] = boundary[i, 0];  //include the value on that raster point
            }

            using (var sw = new StreamWriter(PerilOutput))  //beyond here the code has been shamelessly stolen
            {
                for (int i = 0; i < boundary.GetLength(0); i++)   //for all elements in the output array
                {
                    if (!(output[i, 2] == int.MaxValue))
                    {
                        for (int j = 0; j < 3; j++)
                        {
                            sw.Write((int)output[i, j] + " ");       //write the element in the file
                        }
                        sw.Write("\n");                             //enter new line
                    }
                }
                sw.Flush();                                 //i dont really know
                sw.Close();                                 //close opened output text file
            }
        }

#################################################

public void consoleMatrix(float[,] output) //DEBUG write a matrix on the console
        {
            for (int i = 0; i < output.GetLength(0); i++)       //for all elements in the matrix
            {
                for (int j = 0; j < output.GetLength(1); j++)
                {
                    Console.Write(output[i, j] + " ");          //Write the matrix element
                }
                Console.WriteLine(" ");                         //go to next line
            }
            Console.WriteLine(" ");                             //in the end go to next line
        }

################################################




            /*




            List<int> uniqueBoundary = new List<int>();                         //make a new list that will contain all the nodes whose safetyboundary value is nonzero

            for (int x = 0; x < compoundBoundary.GetLength(0); x++)                //For all the elements in the safetymatrix
            {
                for (int y = 0; y < compoundBoundary.GetLength(1); y++)
                {
                    if (compoundBoundary[x, y] != 0)                               //If the value of the cell is nonzero (thus inside the safety boundary) add it to the list (by linearising its index)
                    {
                        uniqueBoundary.Add(x * compoundBoundary.GetLength(1) + y);
                    }
                }
            }

            int dong=0;

            Console.WriteLine("GETLINE: uniqueBoundary Made");

            List<int> lineBoundary = new List<int>();                           //create a new list to include all nodes NOT on the edge of the boundary (made like this for debugging purposes)
           
            for (int i = 0; i < uniqueBoundary.Count; i++)                      //For every element in the current boundary list
            {
                if (!(uniqueBoundary.Contains(uniqueBoundary.ElementAt(i) + 1) && uniqueBoundary.Contains(uniqueBoundary.ElementAt(i) - 1) && uniqueBoundary.Contains(uniqueBoundary.ElementAt(i) - compoundBoundary.GetLength(1)) && uniqueBoundary.Contains(uniqueBoundary.ElementAt(i) + compoundBoundary.GetLength(1))))
                {
                    //if NOT all neighbors of the target node are in the boundary matrix
                    lineBoundary.Add(uniqueBoundary.ElementAt(i));  //Add the node to the new List
                    dong++;
                    Console.Write("\r DING!" + dong);                
                }
            }
            int[,] output = new int[lineBoundary.Count, 2];                     //Instantiate the output matrix (to be used in [x,y] format)
            int count = 0;

            foreach (int boundaryPoint in lineBoundary)                         //For every boundary point, add its X and Y values to the output matrix
            {
                output[count, 0] = (boundaryPoint % compoundBoundary.GetLength(1))+1;   //Added the +1 since the indices in the cartesian system must start from 1
                output[count, 1] = (boundaryPoint / compoundBoundary.GetLength(1))+1;
                count++;
            }

            return output;   //return the new boundary matrix, should only include the edge nodes
*/


