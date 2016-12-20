~~~
  ___  _  _  ____   __   _  _   __  ____   __  ____  __  ____ 
 / __)/ )( \(  _ \ /  \ ( \/ ) /  \(    \ /  \(  _ \(  )/ ___)
( (__ ) __ ( )   /(  O )/ \/ \(  O )) D ((  O ))   / )( \___ \
 \___)\_)(_/(__\_) \__/ \_)(_/ \__/(____/ \__/(__\_)(__)(____/

Copyright Cameron Newnham 2015-2016
~~~
## General ##
---
### Purpose ###

This library is intended for use with Grasshopper, an extension to the 3D software Rhinoceros 3D. The goal of this library is to provide efficient and simple functionality to extend the creation, usage and display of meshes. 

There are multiple available, in-depth and powerful solutions for the majority of what is covered by this library. This is not intended as a replacement, but rather a simple, efficient, and functional alternative.  

The majority of code in this library is optimized and multithreaded. For example; the voxel sampling algorithm uses a multi-threaded KD-Tree, and also switches compute modes based on historical performance (between sampling per-voxel, or inverse sampling from points).

### Grasshopper usage ###

* As a traditional grasshopper library
* As a code library to be referenced within Grasshopper
* As a hybrid; to be used in Grasshopper, with some modules written as ad-hoc C# components.

## Components ##
---
### Creation ###
* **Mesh Loft**: Creates a welded mesh from lofting polylines that have the same number of control points.
* **Mesh Pipe:** Quickly pipes polylines. Intended to be similar to "!_ApplyCurvePiping"


### Display ###
* **Curvature Approximation:** Displays approximate curvature on an immediate-vertex basis only.
* **Draft Angle Analysis:** Displays draft angles relative to the world plane.


### Isosurfacing ###
* **Sample Voxels:** Automatically creates a bounding box and grid resolution based on numerical input, and generates a grid of voxel values.
* **Sample Voxels (Custom):** As above, but allows a custom box and resolution.
* **Build Isosurface:** Constructs the isosurface based on the above found values. Can use any sampling by taking in a Single[x,y,z] as the input.
* **Close Voxel Data:** Caps the voxel values so that the output volume is ensured to be closed.


### Smoothing ###
* **QuickSmooth:** A very fast smoothing algorithm based on laplacian smoothing. Multi-threaded and optimized.

## Performance ##
---
In a simple benchmark of 40x40x40 cells (64,000 total), sampled to a range of 1.67 cells the results for voxel sampling and surface construction were:

* Cocoon: 2.8s
* Chromodoris: 0.2s
* Millipede: 10.1s
* Dodo: 9.0s

For the smoothing algorithm with a mesh of 400,000 and 1,500,000 faces, and then with 10 iterations:

* Weaverbird's Laplacian Smoothing: 0.65s / 2.6s / 16.8s
* Chromodoris Quicksmooth: 0.19s / 0.6s / 0.9s

This benchmark files are available on the git repository. However, it is also worth noting that intention of each of these libraries may differ; for example, Cocoon extends it's functionality to include Breps and Curves for sampling, whereas Chromodoris offers only points due to the underlying optimizations. Weaverbird's laplacian smoothing implementation is more sophisticated, but not always needed. Furthermore, as I have completed these benchmarks there may be bias as I am more familiar with my own library; you are invited to execute similar tests and comparisons and post the results.


## Licensing and Credits ##
---

### License ###

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.

### Credits ###

This library extends and implements the following libraries:

#### kd-sharp (for KDTree spatial searching) ####
MIT License, https://code.google.com/archive/p/kd-sharp/
Distributed as a compiled DLL, however the exact source code is available upon request.

#### toxiclibs volume (for isosurfacing) ####
LGPL, Karsten Schmidt. https://bitbucket.org/postspectacular/toxiclibs

This library made use of publicly available information from the following:  
Mirco Becker, "Mesh Curvature Analysis for Rhino Grasshopper", python code, http://www.informance-design.com/?p=690