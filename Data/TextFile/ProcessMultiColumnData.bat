rem Single-value alignment (SVA), aka linear regression
..\..\Bin\IMSDriftTimeAligner.exe -BaseStart 27 -Align 0 -mzmin 1632 -mzmax 1638 -maxshift 500 -i AgilentTuneMix_LargePressureFluctuations_Unaligned.txt -o AgilentTuneMix_SVA\Placeholder.txt -DatasetName AgilentTuneMix_LargePressureFluctuations

rem Dynamic time warping (DTW)
..\..\Bin\IMSDriftTimeAligner.exe -BaseStart 27 -Align 1 -DTWShift 25 -ITF 0.005 -i AgilentTuneMix_LargePressureFluctuations_Unaligned.txt -o AgilentTuneMix_DTW_ITF_0.005\Placeholder.txt -DatasetName AgilentTuneMix_LargePressureFluctuations
