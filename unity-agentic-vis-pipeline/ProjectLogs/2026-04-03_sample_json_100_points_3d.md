# 2026-04-03 Sample JSON 100 Points 3D

## Summary

Updated the local test JSON to use a more visible 3D sample dataset with 100 points and a small set of highlighted points.

## Existing Files Modified

- `Assets/StreamingAssets/result.json`

## What Changed

- replaced the small sample payload with a 100-point STC-like 3D point set
- arranged the points across multiple vertical layers
- kept 10 selected points for yellow highlight testing
- kept row-wise links for basic line rendering tests

## Why

The previous tiny sample worked, but it was not ideal for visually validating the frontend rendering pipeline in Unity.

This larger sample is better for:

- verifying automatic JSON-driven rendering
- checking depth / layering in 3D
- validating selection highlighting
- observing denser point distributions
