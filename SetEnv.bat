@echo off

set FB_BIN=%~dp0
set FB_BIN=%FB_BIN:\\=\%

@echo FB_BIN set to %FB_BIN%

SET "PATH=%FB_BIN%;%PATH%"
@echo Path set to %PATH%
SET "GDAL_DATA=%FB_BIN%gdal-data"
@echo GDAL_DATA set to %GDAL_DATA%
SET "PROJ_LIB=%FB_BIN%proj7\SHARE"
@echo PROJ_LIB set to %PROJ_LIB%
