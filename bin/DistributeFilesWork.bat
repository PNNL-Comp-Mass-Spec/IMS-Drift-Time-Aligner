@echo off

IF [%1] == [] GOTO MissingTarget

echo.
echo.
echo Copying files to %1

xcopy IMSDriftTimeAligner.exe %1 /d /y /f
xcopy MathNet.Numerics.dll %1 /d /y /f
xcopy NDtw.dll %1 /d /y /f
xcopy NDtw.Visualization.Wpf.dll %1 /d /y /f
xcopy OxyPlot.dll %1 /d /y /f
xcopy OxyPlot.Wpf.dll %1 /d /y /f
xcopy OxyPlot.Xps.dll %1 /d /y /f
xcopy PRISM.dll %1 /d /y /f
xcopy System.Data.SQLite.dll %1 /d /y /f
xcopy UIMFLibrary.dll %1 /d /y /f
xcopy ..\Readme.md %1 /d /y /f

goto done

:MissingTarget
echo.
echo Error: You must specify a directory when calling this batch file

:Done
