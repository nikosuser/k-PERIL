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
