# k-PERIL

By Nikolaos Kalogeropoulos, HAZELAB research group, Imperial College London. https://www.imperial.ac.uk/people/nikolaos.kalogeropoulos17
December 2023

k-PERIL is a model that calculated probabilistic trigger boundaries around communities under threat of wildfires. It is based on the PERIL algorithm https://zenodo.org/records/4106654. For further information on trigger boundaries, you can refer to:

https://www.sciencedirect.com/science/article/pii/S0379711223001224

https://www.sciencedirect.com/science/article/pii/S0925753522002533

INPUTS:

You need three files in the Input files to run a simulation with k-PERIL. First is the landscape file of your domain, renamed to INPUT.lcp. Second is the fuels layer of that LCP in ascii format, renamed to FUELTEMPLATE.asc. The third file is VARS.txt which contains all the required input parameters to k-PERIL. All of them are explained in comments there. Note that conditioning days refers to the pre-conditioning days of Farsite and Flammap, which is used as a proxy to accurate measurements of the fuel moisture. Values up to 20 are a reasonable tradeoff between accuracy and computational time. Also note that for the points defining the urban area, (0,0) is the top left point, and (1,0) refers to the cell to the right of the origin. 

HOW TO RUN:

k-PERIL can either be run from an executable or from source. In either case, the file structure and files in this rep should always be present when running a simulation.

If run from executable, just run the RoxCaseGen.exe executable in the k-PERIL Exe folder.

If run from source, change the path variable to your target path (where you cloned this rep)

OUTPUTS:

k-PERIL saves the intermediate model outputs in the Median_Outputs folder. Most products of intermediate simulations are deleted, but the rate of spread data are kept for statistical analysis, if needed. In the Output folder, you will find all the individual trigger boundaries, along with the final probabilistic trigger boundaries, in case any are needed for further analysis.

If you find any problems with this model, please report them in this repository or to nikolaos.kalogeropoulos@imperial.ac.uk

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
