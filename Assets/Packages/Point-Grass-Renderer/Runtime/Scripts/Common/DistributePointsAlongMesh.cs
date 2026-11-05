using System.Collections;
using System.Collections.Generic;

using UnityEngine;


namespace MicahW.PointGrass {
    using static PointGrassCommon;

    public static class DistributePointsAlongMesh {
        /// <summary>Distributes points along an input mesh. Technically a setup function for <c>DistributePointsAlongMesh.DistributedPoints_CPU()</c></summary>
        /// <param name="mesh">The input mesh data</param>
        /// <param name="scale">The transformation scale of the mesh</param>
        /// <param name="pointCount">The base number of points to be distributed</param>
        /// <param name="seed">The random seed</param>
        /// <param name="multiplyPointsByArea">If set to <c>true</c>, the point count will be multiplied by the mesh's total area</param>
        /// <param name="forcedNormal">The normal vector the points will be overwritten with</param>
        /// <param name="useColours">Should we use colour inputs?</param>
        /// <param name="useDensity">Should we use density inputs?</param>
        /// <param name="useLength">Should we use length inputs?</param>
        /// <param name="densityCutoff">The density cutoff when density inputs are enabled</param>
        /// <param name="lengthMapping">The mapping range of the length inputs</param>
        /// <returns><c>MeshPoint[]</c> - The distributed mesh points</returns>
        public static MeshPoint[] DistributePoints(MeshData mesh, Vector3 scale, float pointCount, int seed, bool multiplyPointsByArea, Vector3? forcedNormal, bool useColours, bool useDensity, bool useLength, float densityCutoff, Vector2 lengthMapping) {
            if (pointCount <= 0) { return null; } // Since the point count is too low, return nothing
            if (densityCutoff >= 1f) { return null; } // Since the density cutoff is too high (And may cause errors) return nothing

            // Create setup data
            useDensity &= mesh.HasAttributes;
            useLength &= mesh.HasAttributes;

            useColours &= mesh.HasColours;

            // Apply the density cutoff to the mesh data
            if (useDensity) { mesh.ApplyDensityCutoff(densityCutoff); }
            if (useLength) { mesh.ApplyLengthMapping(lengthMapping); }

            // Calculate the cumulative sizes and total area
            float[] cumulativeSizes = GetCumulativeTriSizes(mesh.tris, mesh.verts, mesh.attributes, scale, useDensity, out float totalArea);
            // Calculate the total number of points
            int pointTotal = multiplyPointsByArea ? Mathf.FloorToInt(pointCount * totalArea) : Mathf.FloorToInt(pointCount);
            // If the point total or the total area is 0f, return nothing
            if (pointTotal <= 0 || totalArea <= 0) { return null; }

            // Distribute the points
            return DistributePoints_CPU(mesh, cumulativeSizes, pointTotal, seed, totalArea, forcedNormal, useColours, useLength);
        }
        /// <summary>An inner function that actually distributes the points</summary>
        /// <param name="mesh">The mesh data used for distribution</param>
        /// <param name="cumulativeSizes">The cumulative sizes of each triangles</param>
        /// <param name="pointCount">The total number of distributed points</param>
        /// <param name="seed">The random seed</param>
        /// <param name="totalArea">The total area of the mesh</param>
        /// <param name="forcedNormal">The normal vector the points will be overwritten with</param>
        /// <param name="useLength">Should we use length inputs?</param>
        /// <returns><c>MeshPoint[]</c> - The distributed mesh points</returns>
        private static MeshPoint[] DistributePoints_CPU(MeshData mesh, float[] cumulativeSizes, int pointCount, int seed, float totalArea, Vector3? forcedNormal, bool useColours, bool useLength) {
            // Save the current random state so it can be restored
            Random.State state = Random.state;
            // Init a random state with the set seed
            Random.InitState(seed);
            // Create the points
            MeshPoint[] points = new MeshPoint[pointCount];
            for (int i = 0; i < pointCount; i++) {
                // Find the first triangle index
                float randomSample = Random.Range(0f, totalArea);
                int triIndexA = FindTriangleIndex(cumulativeSizes, randomSample);
                int triIndexB = triIndexA + 1;
                int triIndexC = triIndexA + 2;

                // Get the indices of the triangle vertices
                triIndexA = mesh.tris[triIndexA];
                triIndexB = mesh.tris[triIndexB];
                triIndexC = mesh.tris[triIndexC];

                // Generating barycentric coordinates
                // TODO : Distribute Points Along Mesh - Make the barycentric coordinate generation based on density values of each vertex
                Vector3 BC = new Vector3(Random.value, Random.value, 0f);
                if (BC.x + BC.y >= 1f) {
                    BC.x = 1 - BC.x;
                    BC.y = 1 - BC.y;
                }
                BC.z = 1f - BC.x - BC.y;

                MeshPoint point = new MeshPoint();
                // Positions
                Vector3 P1 = mesh.verts[triIndexA];
                Vector3 P2 = mesh.verts[triIndexB];
                Vector3 P3 = mesh.verts[triIndexC];
                point.position = (P1 * BC.x) + (P2 * BC.y) + (P3 * BC.z);
                // Normals
                if (!forcedNormal.HasValue) {
                    Vector3 N1 = mesh.normals[triIndexA];
                    Vector3 N2 = mesh.normals[triIndexB];
                    Vector3 N3 = mesh.normals[triIndexC];
                    point.normal = (N1 * BC.x) + (N2 * BC.y) + (N3 * BC.z);
                }
                else { point.normal = forcedNormal.Value; }
                // UVs
                Vector2 U1 = mesh.UVs[triIndexA];
                Vector2 U2 = mesh.UVs[triIndexB];
                Vector2 U3 = mesh.UVs[triIndexC];
                point.extraData = (U1 * BC.x) + (U2 * BC.y) + (U3 * BC.z);
                // Colours
                if (useColours) {
                    Color C1 = mesh.colours[triIndexA];
                    Color C2 = mesh.colours[triIndexB];
                    Color C3 = mesh.colours[triIndexC];
                    point.color = (C1 * BC.x) + (C2 * BC.y) + (C3 * BC.z);
                }
                else { point.color = Color.white; }
                // Lengths
                if (useLength) {
                    float L1 = mesh.attributes[triIndexA].y;
                    float L2 = mesh.attributes[triIndexB].y;
                    float L3 = mesh.attributes[triIndexC].y;
                    point.extraData.z = (L1 * BC.x) + (L2 * BC.y) + (L3 * BC.z);
                }
                else { point.extraData.z = 1f; }
                // Random Value
                point.extraData.w = Random.value;

                points[i] = point;
            }
            // Restore the random state
            Random.state = state;
            return points;
        }

        /// <summary>Calculates the cumulative sizes of each triangle from the input data</summary>
        /// <param name="tris">The input triangle data</param>
        /// <param name="verts">The input vertex position data</param>
        /// <param name="attributes">The input colour data. Used when useDensity is enabled</param>
        /// <param name="scale">The input scaling factor</param>
        /// <param name="useDensity">Should we use density inputs to scale the triangle sizes? Requires colour data</param>
        /// <param name="totalArea">The total area of the triangles</param>
        /// <returns><c>float[]</c> - A cumulative array of the triangle sizes</returns>
        private static float[] GetCumulativeTriSizes(int[] tris, Vector3[] verts, Vector2[] attributes, Vector3 scale, bool useDensity, out float totalArea) {
            // If the colours array doesn't match the size of the vertex array, we need to mask useDensity
            useDensity &= attributes != null && attributes.Length == verts.Length;
            // Get the sizes of each triangle
            float[] sizes = useDensity ? GetWeightedTriSizes(tris, verts, attributes, scale) : GetTriSizes(tris, verts, scale);
            // Return nothing if the resulting array is empty
            if (sizes == null || sizes.Length == 0) { totalArea = 0f; return null; }
            // Calculate the total area and accumulate the sizes array (e.g. { 0.1, 0.2, 0.1 } => { 0.1, 0.3, 0.4 } )
            totalArea = sizes[0];
            for (int i = 1; i < sizes.Length; i++) {
                totalArea += sizes[i];
                sizes[i] = totalArea;
            }
            // Return the cumulative sizes
            return sizes;
        }
        /// <summary>Calculates the sizes of each triangle from the input data</summary>
        /// <param name="tris">The input triangle data</param>
        /// <param name="verts">The input vertex position data</param>
        /// <param name="scale">The input scaling factor</param>
        /// <returns><c>float[]</c> - An array of the triangle sizes</returns>
        private static float[] GetTriSizes(int[] tris, Vector3[] verts, Vector3 scale) {
            int triCount = tris.Length / 3;
            float[] sizes = new float[triCount];
            for (int i = 0; i < triCount; i++) {
                int vertA = tris[i * 3 + 1];
                int vertB = tris[i * 3];
                int vertC = tris[i * 3 + 2];

                Vector3 vecB = verts[vertA] - verts[vertB]; vecB.Scale(scale);
                Vector3 vecC = verts[vertC] - verts[vertB]; vecC.Scale(scale);

                Vector3 cross = Vector3.Cross(vecB, vecC);
                sizes[i] = .5f * cross.magnitude;
            }
            return sizes;
        }
        /// <summary>Calculates the sizes of each triangle from the input data, weighted by the density value in the colour data</summary>
        /// <param name="tris">The input triangle data</param>
        /// <param name="verts">The input vertex position data</param>
        /// <param name="attributes">The input colour data</param>
        /// <param name="scale">The input scaling factor</param>
        /// <returns><c>float[]</c> - An array of the triangle sizes, weighted by the colour data</returns>
        private static float[] GetWeightedTriSizes(int[] tris, Vector3[] verts, Vector2[] attributes, Vector3 scale) {
            int triCount = tris.Length / 3;
            float[] sizes = new float[triCount];
            for (int i = 0; i < triCount; i++) {
                int vertA = tris[i * 3 + 1];
                int vertB = tris[i * 3];
                int vertC = tris[i * 3 + 2];
                float factor = (attributes[vertA].x + attributes[vertB].x + attributes[vertC].x) / 3f;

                Vector3 vecB = verts[vertA] - verts[vertB]; vecB.Scale(scale);
                Vector3 vecC = verts[vertC] - verts[vertB]; vecC.Scale(scale);

                sizes[i] = factor * .5f * Vector3.Cross(vecB, vecC).magnitude;
            }
            return sizes;
        }
        /// <summary>Uses a binary search to find the matching triangle of a random sample</summary>
        /// <param name="cumulativeTriSizes">Cumulative sizes of each triangle. Each element should be larger than the preceding element</param>
        /// <param name="randomSample">The randomly sampled value</param>
        /// <returns><c>int</c> - The index of the matching triangle</returns>
        private static int FindTriangleIndex(float[] cumulativeTriSizes, float randomSample) {
            int low = 0;
            int high = cumulativeTriSizes.Length - 1;
            while (low < high) {
                int mid = (low + high) / 2;
                if (cumulativeTriSizes[mid] > randomSample) { high = mid; }
                else { low = mid + 1; }
            }
            int triIndex = low * 3;
            return triIndex;
        }
    }
}