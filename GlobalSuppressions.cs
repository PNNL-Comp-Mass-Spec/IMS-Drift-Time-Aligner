﻿// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Usage", "RCS1246:Use element access.", Justification = "Prefer to use .First()", Scope = "module")]
[assembly: SuppressMessage("Readability", "RCS1123:Add parentheses when necessary.", Justification = "Parentheses not needed", Scope = "member", Target = "~M:IMSDriftTimeAligner.clsBinarySearchFindNearest.InterpolateY(System.Double,System.Double,System.Double,System.Double,System.Double)~System.Double")]
[assembly: SuppressMessage("Readability", "RCS1123:Add parentheses when necessary.", Justification = "Parentheses not needed", Scope = "member", Target = "~M:IMSDriftTimeAligner.DriftTimeAlignmentEngine.AlignFrameDataLinearRegression(System.Int32,System.Collections.Generic.IReadOnlyList{System.Double},System.Collections.Generic.List{System.Double},System.Collections.Generic.IEnumerable{System.Int32},IMSDriftTimeAligner.StatsWriter)~System.Collections.Generic.Dictionary{System.Int32,System.Int32}")]
[assembly: SuppressMessage("Readability", "RCS1123:Add parentheses when necessary.", Justification = "Parentheses not needed", Scope = "member", Target = "~M:IMSDriftTimeAligner.DriftTimeAlignmentEngine.GetBaseFrames(UIMFLibrary.DataReader,IMSDriftTimeAligner.FrameAlignmentOptions)~System.Collections.Generic.List{System.Int32}")]
[assembly: SuppressMessage("Readability", "RCS1123:Add parentheses when necessary.", Justification = "Parentheses not needed", Scope = "member", Target = "~M:IMSDriftTimeAligner.DriftTimeAlignmentEngine.ComputeFilteredTICAndBPI(UIMFLibrary.DataReader,UIMFLibrary.ScanInfo,System.Double,System.Double)")]
[assembly: SuppressMessage("Simplification", "RCS1173:Use coalesce expression instead of 'if'.", Justification = "Leave as-is for readability", Scope = "member", Target = "~P:IMSDriftTimeAligner.FrameAlignmentOptions.MinimumIntensityThresholdFraction")]
[assembly: SuppressMessage("Simplification", "RCS1179:Unnecessary assignment.", Justification = "Leave as-is for readability", Scope = "member", Target = "~M:IMSDriftTimeAligner.DriftTimeAlignmentEngine.AlignFrameTICToBase(System.Int32,System.Collections.Generic.IReadOnlyList{UIMFLibrary.ScanInfo},System.Collections.Generic.IReadOnlyList{UIMFLibrary.ScanInfo},System.Collections.Generic.IReadOnlyList{System.Int32},IMSDriftTimeAligner.StatsWriter,System.String,System.IO.DirectoryInfo)~System.Collections.Generic.Dictionary{System.Int32,System.Int32}")]
[assembly: SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Allowed name", Scope = "type", Target = "~T:IMSDriftTimeAligner.clsBinarySearchFindNearest")]
