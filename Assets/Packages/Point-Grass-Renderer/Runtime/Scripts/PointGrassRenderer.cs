using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace MicahW.PointGrass {
    using static DistributePointsAlongMesh;
    using static PointGrassCommon;

    [ExecuteAlways]
    public class PointGrassRenderer : MonoBehaviour {
        [Header("Distribution Parameters")]
        public DistributionSource distSource = DistributionSource.Mesh;
        public Mesh baseMesh = default;
        public TerrainData terrain;
        public TerrainLayer[] terrainLayers;
        public Vector2Int chunkCount = new Vector2Int(8, 8);

        public MeshFilter[] sceneFilters;

        private MeshFilter filter;

        [Header("Grass Parameters")]
        public BladeType bladeType = BladeType.Flat;
        public bool multipleMeshes = false;
        private bool UsingMultipleMeshes => bladeType == BladeType.Mesh && multipleMeshes;
        public Mesh grassBladeMesh = default;
        public Mesh[] grassBladeMeshes = new Mesh[1];
        public float[] meshDensityValues = new float[] { 1f };

        public bool multipleMaterials = false;
        public Material material = default;
        public Material[] materials = new Material[1];

        public UnityEngine.Rendering.ShadowCastingMode shadowMode = UnityEngine.Rendering.ShadowCastingMode.On;
        public SingleLayer renderLayer;

        [Header("Point Parameters")]
        public float pointCount = 1000f;
        public bool multiplyByArea = false;
        [Range(0f, 1f)] public float pointLODFactor = 1f;

        public bool randomiseSeed = true;
        public int seed = 0;

        public bool overwriteNormalDirection;
        public Vector3 forcedNormal = Vector3.up;

        public bool useDensity = true;
        [Range(0f, 1f)] public float densityCutoff = 0.5f;
        public bool useLength = true;
        public Vector2 lengthMapping = new Vector2(0f, 1f);

        // Single Mesh Fields
        private ComputeBuffer pointBuffer;
        private MaterialPropertyBlock materialBlock;
        private Bounds boundingBox;
        // Multiple Mesh Fields
        private ComputeBuffer[] pointBuffers;
        private MaterialPropertyBlock[] materialBlocks;
        private Bounds[] boundingBoxes;

        [Header("Projection Parameters")]
        public ProjectionType projectType;
        public LayerMask projectMask = ~0;

        [Header("Bounding Box Parameters")]
        public Bounds boundingBoxOffset = new Bounds(Vector3.zero, Vector3.one);



#if UNITY_EDITOR
        private void OnDrawGizmosSelected() {
            Bounds renderBounds = AddBoundsExtrusion(TransformBounds(GetLocalBounds()));
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(renderBounds.center, renderBounds.size);

            if (distSource == DistributionSource.Mesh && baseMesh != null) {
                Gizmos.color = Color.cyan;
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawWireMesh(baseMesh);

                Gizmos.matrix = Matrix4x4.identity;
            }

            if (distSource == DistributionSource.TerrainData && terrain != null) {
                Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
                Gizmos.matrix = transform.localToWorldMatrix;

                Vector3 size = terrain.size;

                for (int x = 1; x < chunkCount.x; x++) { Gizmos.DrawLine(new Vector3((float)x / chunkCount.x * size.x, size.y * 0.25f, 0f), new Vector3((float)x / chunkCount.x * size.x, size.y * 0.25f, size.z)); }
                for (int y = 1; y < chunkCount.y; y++) { Gizmos.DrawLine(new Vector3(0f, size.y * 0.25f, (float)y / chunkCount.y * size.z), new Vector3(size.x, size.y * 0.25f, (float)y / chunkCount.y * size.z)); }
                Gizmos.matrix = Matrix4x4.identity;

                if (boundingBoxes != null) {
                    Gizmos.color = new Color(0.5f, 1f, 1f, 1f);
                    for (int i = 0; i < boundingBoxes.Length; i++) {
                        Bounds bound = AddBoundsExtrusion(TransformBounds(boundingBoxes[i]));
                        Gizmos.DrawWireCube(bound.center, bound.size);
                    }
                }
            }
        }

        private void OnValidate() {
            if (UsingMultipleMeshes && grassBladeMeshes != null) {
                // Get the number of grass blade meshes
                int grassMeshCount = grassBladeMeshes.Length;
                // Update the density array to match the length of the grass blade array
                if (meshDensityValues == null) {
                    meshDensityValues = new float[grassMeshCount];
                    for (int i = 0; i < grassMeshCount; i++) { meshDensityValues[i] = 1f; }
                }
                else if (meshDensityValues.Length != grassMeshCount) {
                    System.Array.Resize(ref meshDensityValues, grassMeshCount);
                }
                // Update the materials array to match the length of the grass blade array
                if (materials == null) { materials = new Material[grassMeshCount]; }
                else if (multipleMaterials && materials.Length != grassMeshCount) {
                    System.Array.Resize(ref materials, grassMeshCount);
                }
            }
        }
#endif

        private void Reset() {
            ClearBuffers();

            bladeType = BladeType.Flat;
            multipleMeshes = false;

            grassBladeMesh = null;
            grassBladeMeshes = new Mesh[1];
            meshDensityValues = new float[] { 1f };
            multipleMaterials = false;

            material = null;
            materials = new Material[1];
            shadowMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderLayer.Set(gameObject.layer);

            pointCount = 1000f;
            multiplyByArea = false;
            pointLODFactor = 1f;

            randomiseSeed = true;
            seed = 0;

            overwriteNormalDirection = false;
            forcedNormal = Vector3.up;

            useDensity = true;
            densityCutoff = 0.5f;
            useLength = true;

            projectType = ProjectionType.None;
            projectMask = ~0;
        }

        private void OnEnable() {
            // If the compatibility check fails, return to prevent further execution
            if (!CompatibilityCheck()) return;
            // Find the property IDs if they haven't been found yet
            if (!PropertyIDsInitialized) { FindPropertyIDs(); }
            // Generate the grass blade mesh if it isn't active
            if (grassMeshFlat == null || grassMeshCyl == null) { GenerateGrassMeshes(); }

            BuildPoints();
        }
        private void OnDisable() { ClearBuffers(); }

        // TODO : Point Grass Renderer - Look into implementing the drawing routine as a custom renderer so the grass will still be drawn while the game is paused in the editor
        private void LateUpdate() { DrawGrass(); }

        /// <summary>Checks if the system supports the necessary features used by the point grass</summary>
        /// <returns><c>bool</c> - Equals <c>true</c> if the system supports all the required features</returns>
        private bool CompatibilityCheck() {
            // Helper function with preprocessor directives to disable/destroy this component
            void Disable() {
#if UNITY_EDITOR
                enabled = false;
#else
                Destroy(this);
#endif
            }

            // If the system doesn't support instancing, write to the console and destroy this component
            if (!SystemInfo.supportsInstancing) {
                Debug.LogError($"This system doesn't support instanced draw calls. \"{gameObject.name}\" is unable to render its point grass");
                Disable();
                return false;
            }
            // If the system's shader level is too low, write to the console and destroy this component
            else if (SystemInfo.graphicsShaderLevel < 45) {
                Debug.LogError($"This system doesn't support shader model 4.5. Compute buffers are unsupported in the point grass shaders");
                Disable();
                return false;
            }

            return true;
        }

        /// <summary>Clears any existing buffers and builds new ones</summary>
        public void BuildPoints() {
            // Clear Buffers
            ClearBuffers();
            // Get the seed
            int seed = randomiseSeed ? Random.Range(int.MinValue, int.MaxValue) : this.seed;
            // Create the overwrite normal value
            Vector3? overwriteNormal = null;
            if (overwriteNormalDirection) {
                // Normalize the forced normal
                forcedNormal = forcedNormal.normalized;
                overwriteNormal = transform.InverseTransformDirection(forcedNormal);
            }
            // Build the points
            if (distSource == DistributionSource.TerrainData && chunkCount.x > 1 && chunkCount.y > 1) { BuildPoints_Terrain(seed, overwriteNormal); }
            else { BuildPoints_Mesh(seed, overwriteNormal); }
        }
        /// <summary>Builds the point buffers using mesh data</summary>
        /// <param name="seed">The random/preset seed</param>
        private void BuildPoints_Mesh(int seed, Vector3? overwriteNormal) {
            // If we cannot get the mesh data, return to prevent further execution
            if (!GetMeshData(out MeshData mesh)) { return; }
            // Project the mesh
            if (projectType == ProjectionType.ProjectMesh) { ProjectBaseMesh(ref mesh, projectMask, transform); }
            boundingBox = mesh.bounds;

            // Distribute points
            MeshPoint[] points = DistributePoints(mesh, transform.localScale, pointCount, seed, multiplyByArea, overwriteNormal, true, useDensity, useLength, densityCutoff, lengthMapping);
            // Create new buffers
            if (points != null && points.Length > 0) { CreateBuffers(points); }
        }
        /// <summary>Builds the point buffers using terrain data</summary>
        /// <param name="seed">The random/preset seed</param>
        private void BuildPoints_Terrain(int seed, Vector3? overwriteNormal) {
            // Lists that will be converted to arrays after building the points
            List<ComputeBuffer> bufferList = new List<ComputeBuffer>();
            List<MaterialPropertyBlock> blockList = new List<MaterialPropertyBlock>();
            List<Bounds> boundsList = new List<Bounds>();
            // Calculating the chunk size
            Vector3 chunkSize = terrain.size;
            chunkSize.x /= chunkCount.x;
            chunkSize.z /= chunkCount.y;
            // Cache the terrain data for the terrain generation
            CacheTerrainData(terrain, terrainLayers);
            for (int x = 0; x < chunkCount.x; x++) {
                for (int y = 0; y < chunkCount.y; y++) {
                    // Get the mesh data for this chunk
                    bool validMesh = GetTerrainMeshData(out MeshData data, new Vector2Int(x, y));
                    if (!validMesh) { continue; }
                    // Generate a unique seed for each chunk
                    int newSeed = seed + x + y * chunkCount.x;
                    // Distribute the points
                    MeshPoint[] points = DistributePoints(data, transform.localScale, pointCount, newSeed, multiplyByArea, overwriteNormal, true, useDensity, useLength, -1f, lengthMapping);
                    if (points == null || points.Length == 0) { continue; }

                    // Create buffers from the computed points
                    if (UsingMultipleMeshes) {
                        // We're using multiple meshes, so we need to split the buffers into separate parts
                        CreateBuffersFromPoints_Multi(points, out ComputeBuffer[] buffers, out MaterialPropertyBlock[] blocks);
                        // If the buffers are null, then there was an error when splitting the buffers
                        if (buffers == null) { continue; }
                        bufferList.AddRange(buffers);
                        blockList.AddRange(blocks);
                    }
                    else {
                        // Create a single buffer
                        CreateBufferFromPoints(points, out ComputeBuffer buffer, out MaterialPropertyBlock block);
                        bufferList.Add(buffer);
                        blockList.Add(block);
                    }
                    // Add the chunk's bounds to the list
                    Bounds bounds = new Bounds(points[0].position, Vector3.zero);
                    for (int i = 1; i < points.Length; i++) { bounds.Encapsulate(points[i].position); }
                    boundsList.Add(bounds);
                }
            }
            // Store the lists into arrays
            if (bufferList.Count > 0) {
                pointBuffers = bufferList.ToArray();
                materialBlocks = blockList.ToArray();
                boundingBoxes = boundsList.ToArray();
            }
        }
        /// <summary>Retrieves/creates mesh data based on the renderer's parameters</summary>
        /// <param name="meshData">The output mesh data</param>
        /// <returns><c>bool</c> - Equals true if the output mesh data is valid</returns>
        private bool GetMeshData(out MeshData meshData) {
            meshData = MeshData.Empty;
            switch (distSource) {
                case DistributionSource.Mesh:
                    if (baseMesh == null) { goto case DistributionSource.MeshFilter; }
                    // Convert the mesh's colours into Vector2s
                    Vector2[] meshAttributes = new Vector2[baseMesh.vertexCount];
                    if (baseMesh.colors != null && baseMesh.colors.Length == baseMesh.vertexCount && (useDensity || useLength)) {
                        for (int i = 0; i < meshAttributes.Length; i++) { meshAttributes[i] = new Vector2(baseMesh.colors[i].r, baseMesh.colors[i].g); }
                    }
                    else { for (int i = 0; i < meshAttributes.Length; i++) { meshAttributes[i] = Vector2.one; } }
                    // Create the mesh data
                    meshData = new MeshData(baseMesh.vertices, baseMesh.normals, baseMesh.uv, baseMesh.triangles, meshAttributes);
                    return true;

                case DistributionSource.MeshFilter:
                    filter = GetComponent<MeshFilter>();
                    if (filter != null) {
                        baseMesh = filter.sharedMesh;
                        if (baseMesh != null) {
                            // Convert the mesh's colours into Vector2s
                            Vector2[] filterAttributes = new Vector2[baseMesh.vertexCount];
                            if (baseMesh.colors != null && baseMesh.colors.Length == baseMesh.vertexCount && (useDensity || useLength)) {
                                for (int i = 0; i < filterAttributes.Length; i++) { filterAttributes[i] = new Vector2(baseMesh.colors[i].r, baseMesh.colors[i].g); }
                            }
                            else { for (int i = 0; i < filterAttributes.Length; i++) { filterAttributes[i] = Vector2.one; } }
                            // Create the mesh data
                            meshData = new MeshData(baseMesh.vertices, baseMesh.normals, baseMesh.uv, baseMesh.triangles, filterAttributes);
                            return true;
                        }
                    }
                    break;

                case DistributionSource.TerrainData:
                    CacheTerrainData(terrain, terrainLayers);
                    return GetTerrainMeshData(out meshData, Vector2Int.zero);

                case DistributionSource.SceneFilters:
                    if (sceneFilters != null && sceneFilters.Length > 0) {
                        meshData = CreateMeshFromFilters(transform, sceneFilters);
                        if (meshData.verts.Length > 0) { return true; }
                    }
                    break;
            }

            return false;
        }
        /// <summary>Gets a generated mesh from terrain data. Requires the terrain data to be cached first with 'CacheTerrainData()'</summary>
        /// <param name="meshData">The output mesh data</param>
        /// <param name="chunkCoord">The coordinate of the chunk</param>
        /// <returns><c>bool</c> - Equals <c>true</c> if the mesh was successfully generated</returns>
        private bool GetTerrainMeshData(out MeshData meshData, Vector2Int chunkCoord) {
            meshData = MeshData.Empty;

            if (terrain != null && terrainLayers != null) {
                int terrainSize = terrain.heightmapResolution;
                int startX = Mathf.FloorToInt((float)terrainSize * chunkCoord.x / chunkCount.x);
                int startY = Mathf.FloorToInt((float)terrainSize * chunkCoord.y / chunkCount.y);
                int endX = Mathf.CeilToInt((float)terrainSize * (chunkCoord.x + 1) / chunkCount.x);
                int endY = Mathf.CeilToInt((float)terrainSize * (chunkCoord.y + 1) / chunkCount.y);

                float denCutoff = useDensity ? densityCutoff : 0f;
                meshData = CreateMeshFromTerrainData(terrain, denCutoff, startX, startY, endX - startX, endY - startY);

                return true;
            }

            return false;
        }
        
        /// <summary>Creates the renderer's buffers from an array of type <c>MeshPoint</c></summary>
        /// <param name="points">The array of points used to create the buffers</param>
        private void CreateBuffers(MeshPoint[] points) {
            if (UsingMultipleMeshes) { CreateBuffersFromPoints_Multi(points, out pointBuffers, out materialBlocks); }
            else { CreateBufferFromPoints(points, out pointBuffer, out materialBlock); }
        }
        /// <summary>Clears all the buffers and arrays from the renderer</summary>
        private void ClearBuffers() {
            if (pointBuffer != null) { pointBuffer.Release(); }
            if (pointBuffers != null) {
                for (int i = 0; i < pointBuffers.Length; i++) { pointBuffers[i].Release(); }
                pointBuffers = null;
            }
            if (boundingBoxes != null) { boundingBoxes = null; }
        }

        /// <summary>Creates a compute buffer and material property block from an array of type <c>MeshPoint</c></summary>
        /// <param name="points">The array of points</param>
        /// <param name="buffers">The output compute buffer</param>
        /// <param name="blocks">The output material property block</param>
        private void CreateBufferFromPoints(MeshPoint[] points, out ComputeBuffer buffer, out MaterialPropertyBlock block) {
            // Check if the points array is valid
            if (points == null || points.Length == 0) { buffer = null; block = null; return; }
            // Create a new compute buffer
            buffer = new ComputeBuffer(points.Length, MeshPoint.stride);
            // Set the buffer data
            buffer.SetData(points);
            // Create the material property block
            block = CreateMaterialPropertyBlock(buffer);
        }
        /// <summary>Creates multiple buffers and material property blocks from an array of type <c>MeshPoint</c></summary>
        /// <param name="points">The array of points</param>
        /// <param name="buffers">The output compute buffers</param>
        /// <param name="blocks">The output material property blocks</param>
        private void CreateBuffersFromPoints_Multi(MeshPoint[] points, out ComputeBuffer[] buffers, out MaterialPropertyBlock[] blocks) {
            // Check if the points array is valid
            if (points == null || points.Length == 0) { buffers = null; blocks = null; return; }
            // Local copies of the array lengths for readability
            int pointCount = points.Length;
            int meshCount = grassBladeMeshes.Length;

            if (pointCount < meshCount) { buffers = null; blocks = null; } // Since some buffers would have a size of 0, return nothing to prevent errors
            else {
                // Normalize the density values of each mesh by dividing it by the sum
                float sum = 0f;
                float[] densities = new float[meshCount];
                for (int i = 0; i < meshCount; i++) { sum += meshDensityValues[i]; }
                if (sum <= 0) { // Sum is zero. Default to (1f / meshCount) to prevent divide by zero errors
                    float val = 1f / meshCount;
                    for (int i = 0; i < meshCount; i++) { densities[i] = val; }
                }
                else for (int i = 0; i < meshCount; i++) { densities[i] = meshDensityValues[i] / sum; } // Sum is greater than 0. Use a divide

                // Create the buffer and block arrays
                buffers = new ComputeBuffer[meshCount];
                blocks = new MaterialPropertyBlock[meshCount];

                // Fill the buffers
                int dataPointer = 0;
                for (int i = 0; i < meshCount; i++) {
                    int targetCount = Mathf.RoundToInt(pointCount * densities[i]); // The target size of the buffer
                    int remainingPoints = pointCount - dataPointer; // The remaining number of points
                    int remainingBuffers = (meshCount - i) - 1; // The number of remaining buffers (excluding the buffer we're creating)

                    // Calculate the size of this buffer so it uses at least 1 point and leaves 1 points for the remaining buffers
                    int bufferSize = Mathf.Max(1, Mathf.Min(remainingPoints - (remainingBuffers), targetCount)); // The final size of the buffer

                    buffers[i] = new ComputeBuffer(bufferSize, MeshPoint.stride); // Create the buffer
                    buffers[i].SetData(points, dataPointer, 0, bufferSize); // Fill the buffer with the next set of points
                    blocks[i] = CreateMaterialPropertyBlock(buffers[i]); // Create a material property block

                    dataPointer += bufferSize; // Increase the data pointer
                }
            }
        }
        
        /// <summary>Draws the grass</summary>
        private void DrawGrass() {
            // If there's no buffers to draw, return
            if (pointBuffer == null && pointBuffers == null) { return; }

            Mesh mesh = GetGrassMesh();
            Material mat = material;
            Bounds finalBounds = TransformBounds(GetLocalBounds());

            bool hasPointBuffers = pointBuffers != null && pointBuffers.Length > 0;
            bool useMultipleMeshes = UsingMultipleMeshes && grassBladeMeshes != null && hasPointBuffers;
            bool useMultipleMats = multipleMaterials && materials != null && hasPointBuffers;

            // If there are multiple bounding boxes
            if (boundingBoxes != null && boundingBoxes.Length > 0) {
                if (hasPointBuffers) {
                    // The number of compute buffers drawn for each bounding box
                    int buffersPerBounds = pointBuffers.Length / boundingBoxes.Length;
                    // Update the booleans for multiple meshes and materials
                    useMultipleMeshes &= grassBladeMeshes.Length >= buffersPerBounds;
                    useMultipleMats &= materials.Length >= buffersPerBounds;

                    // For each chunk
                    for (int i = 0; i < boundingBoxes.Length; i++) {
                        finalBounds = TransformBounds(boundingBoxes[i]);
                        // For each buffer in the chunk
                        for (int j = 0; j < buffersPerBounds; j++) {
                            int index = i * buffersPerBounds + j;
                            // If we're using multiple meshes, update the drawn mesh
                            if (useMultipleMeshes) {
                                mesh = grassBladeMeshes[j];
                                // If we're using multiple materials, update the material
                                if (useMultipleMats) { mat = materials[j]; }
                            }
                            DrawGrassBuffer(pointBuffers[index], ref materialBlocks[index], mesh, mat, finalBounds);
                        }
                    }
                }
                // Backup for scene filters
                else { DrawGrassBuffer(pointBuffer, ref materialBlock, mesh, mat, TransformBounds(boundingBoxes[0])); }
            }
            // If we have multiple point buffers
            else if (hasPointBuffers) {
                // Update the booleans for multiple meshes and materials
                useMultipleMeshes &= grassBladeMeshes.Length >= pointBuffers.Length;
                useMultipleMats &= materials.Length >= pointBuffers.Length;
                // For each buffer
                for (int i = 0; i < pointBuffers.Length; i++) {
                    if (useMultipleMeshes) {
                        mesh = grassBladeMeshes[i];
                        if (useMultipleMats) { mat = materials[i]; }
                    }
                    DrawGrassBuffer(pointBuffers[i], ref materialBlocks[i], mesh, mat, finalBounds);
                }
            }
            // If we only have the one point buffer
            else { DrawGrassBuffer(pointBuffer, ref materialBlock, mesh, mat, finalBounds); }
            
        }
        /// <summary>Draws a single grass buffer</summary>
        /// <param name="buffer">The buffer of type <c>MeshPoint</c> used for drawing</param>
        /// <param name="block">A reference to the buffer's corresponding <c>MaterialPropertyBlock</c></param>
        /// <param name="mesh">The mesh to be drawn for each <c>MeshPoint</c></param>
        /// <param name="mat">The material used for rendering</param>
        /// <param name="bounds">The bounding box</param>
        private void DrawGrassBuffer(ComputeBuffer buffer, ref MaterialPropertyBlock block, Mesh mesh, Material mat, Bounds bounds) {
            if (buffer == null || !buffer.IsValid()) { return; }
            int count = Mathf.CeilToInt(buffer.count * pointLODFactor);
            // If the mesh isn't null, the material isn't null, and the material property block isn't null, the point count is greater than 0, draw the buffer
            if (mesh != null && mat != null && block != null && count > 0) {
                // Apply the offset to the bounding box
                bounds = AddBoundsExtrusion(bounds);
                UpdateMaterialPropertyBlock(ref block, transform);
                // Draw the grass, using the generated MaterialPropertyBlock to supply buffer data
                Graphics.DrawMeshInstancedProcedural(mesh, 0, mat, bounds, count, block, shadowMode, true, renderLayer.LayerIndex, null);
            }

        }
        /// <summary> Creates a <c>MaterialPropertyBlock</c> used with rendering the grass meshes </summary>
        /// <param name="pointBuffer">The compute buffer supplied to the material property block</param>
        /// <returns><c>MaterialPropertyBlock</c> - A new material property block with the supplied compute buffer</returns>
        private MaterialPropertyBlock CreateMaterialPropertyBlock(ComputeBuffer pointBuffer) {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            // Set mesh point buffer
            block.SetBuffer(ID_PointBuff, pointBuffer);
            // Set matrix parameters
            UpdateMaterialPropertyBlock(ref block, transform);
            // Return the property block
            return block;
        }

        /// <summary>Retrieves a blade mesh depending on the renderer's blade type</summary>
        /// <returns><c>Mesh</c> - The blade mesh</returns>
        private Mesh GetGrassMesh() {
            switch (bladeType) {
                case BladeType.Flat:        return grassMeshFlat;
                case BladeType.Cylindrical: return grassMeshCyl;
                case BladeType.Mesh:        return grassBladeMesh;
                default:                    throw new System.ArgumentException(message: "Invalid enum value");
            };
        }
        /// <summary>Gets the local bounding box based on the distribution source and renderer state</summary>
        /// <returns><c>Bounds</c> - The bounding box in local space</returns>
        public Bounds GetLocalBounds() {
            switch (distSource) {
                case DistributionSource.Mesh:
                    return boundingBox;

                case DistributionSource.MeshFilter:
                    goto case DistributionSource.Mesh;

                case DistributionSource.TerrainData:
                    if (terrain == null) { goto default; }
                    else { return new Bounds(terrain.size * 0.5f, terrain.size); }

                case DistributionSource.SceneFilters:
                    return boundingBox;

                default:
                    return new Bounds(Vector3.zero, Vector3.one);
            }
        }
        /// <summary>Transforms the bounding box based on the bounding box's corners</summary>
        /// <param name="localBounds">The bounding box in local space</param>
        /// <returns><c>Bounds</c> - The bounding box transformed from the renderer's local space to world space</returns>
        private Bounds TransformBounds(Bounds localBounds) {
            // Get the min and max points
            Vector3 min = localBounds.min;
            Vector3 max = localBounds.max;
            // Get all the corner points
            Vector3[] points = new Vector3[] {
                min, max,

                new Vector3(max.x, min.y, min.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, min.y, max.z),

                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
            };
            // Transform all the points
            for (int i = 0; i < points.Length; i++) { points[i] = transform.TransformPoint(points[i]); }
            // Create the new bounds
            localBounds = new Bounds(points[0], Vector3.zero);
            for (int i = 1; i < points.Length; i++) { localBounds.Encapsulate(points[i]); }
            // Return the bounding box
            return localBounds;
        }
        /// <summary>Applys <c>boundingBoxOffset</c> to the input bounding box</summary>
        /// <param name="worldSpaceBounds">The input bounding box</param>
        /// <returns><c>Bounds</c> - The extruded bounding box</returns>
        private Bounds AddBoundsExtrusion(Bounds worldSpaceBounds) {
            worldSpaceBounds.center += boundingBoxOffset.center;
            worldSpaceBounds.size += boundingBoxOffset.size;
            return worldSpaceBounds;
        }


        // TODO : Point Grass Renderer - Double-check these set functions
        #region Parameter Update Methods
        public void SetDistributionSource(Mesh mesh) {
            if (mesh == null) { Debug.LogError($"An attempt was made to set the distribution source on \"{gameObject.name}\" to null. Make sure the input distribution source is not null"); return; }
            distSource = DistributionSource.Mesh; baseMesh = mesh; // Set the distribution source and base mesh
        }
        public void SetDistributionSource(MeshFilter filter) {
            if (filter == null) { Debug.LogError($"An attempt was made to set the distribution source on \"{gameObject.name}\" to null. Make sure the input distribution source is not null"); return; }

            if (filter.gameObject == gameObject) { distSource = DistributionSource.MeshFilter; } // If the filter is on this object, then we just need to set the distribution source (since the MeshFilter distribution source doesn't keep a reference to the filter)
            else { SetDistributionSource(new MeshFilter[] { filter }); } // Since the filter isn't on this object, we need to use the scene filters distribution source
        }
        public void SetDistributionSource(TerrainData terrain) {
            if (terrain == null) { Debug.LogError($"An attempt was made to set the distribution source on \"{gameObject.name}\" to null. Make sure the input distribution source is not null"); return; }
            distSource = DistributionSource.TerrainData; this.terrain = terrain; // Set the distribution source and terrain data
        }
        public void SetDistributionSource(MeshFilter[] sceneFilters) {
            if (sceneFilters == null) { Debug.LogError($"An attempt was made to set the distribution source on \"{gameObject.name}\" to null. Make sure the input distribution source is not null"); return; }
            distSource = DistributionSource.SceneFilters; this.sceneFilters = sceneFilters; // Set the distribution source and scene filter references
        }

        public void SetBladeType(BladeType type) { bladeType = type; }
        public void SetBladeMesh(Mesh mesh) {
            if (mesh == null) { Debug.LogError($"An attempt was made to set the blade mesh on \"{gameObject.name}\" to null or an empty array. Make sure the input blade mesh is not null"); return; }
            // Set the blade mesh. And make sure multiple meshes is disabled
            SetBladeType(BladeType.Mesh);
            grassBladeMesh = mesh;
            multipleMeshes = false;
        }
        public void SetBladeMesh(Mesh[] meshes, float[] meshDensityValues = null, Material[] materials = null) {
            // Check if the meshes are valid
            if (meshes == null || meshes.Length == 0) { Debug.LogError($"An attempt was made to set the blade meshes on \"{gameObject.name}\" to null or an empty array. Make sure the input blade meshes are not null"); return; }

            if (meshes.Length == 1) { SetBladeMesh(meshes[0]); } // If there's only one mesh in the array, use the normal mesh blade mode instead
            else {
                // Set the blade type, meshes and enable multiple meshes
                SetBladeType(BladeType.Mesh);
                grassBladeMeshes = meshes;
                multipleMeshes = true;
                // Set mesh density values, resizing if necessary
                if (meshDensityValues != null && meshDensityValues.Length > 0) { this.meshDensityValues = meshDensityValues; }
                if (this.meshDensityValues.Length != meshes.Length) { System.Array.Resize(ref this.meshDensityValues, meshes.Length); }
                // Set materials
                if (materials != null && materials.Length > 0) { SetMaterials(materials); }
            }
        }
        public void SetBladeDensities(float[] densities) {
            if (densities == null || densities.Length == 0) { Debug.LogError($"An attempt was made to set the blade densities on \"{gameObject.name}\" to null or an empty array. Make sure the input blade densities are not null"); return; }

            if (densities.Length != grassBladeMeshes.Length) { System.Array.Resize(ref densities, grassBladeMeshes.Length); }
            meshDensityValues = densities;
        }
        public void SetMaterial(Material mat) {
            if (mat == null) { Debug.LogError($"An attempt was made to set the blade material on \"{gameObject.name}\" to null or an empty array. Make sure the input blade material is not null"); return; }

            multipleMaterials = false;
            material = mat;
        }
        public void SetMaterials(Material[] materials) {
            if (materials == null || materials.Length == 0) { Debug.LogError($"An attempt was made to set the blade materials on \"{gameObject.name}\" to null or an empty array. Make sure the input blade materials are not null"); return; }

            if (materials.Length == 1) { SetMaterial(materials[0]); }
            else {
                multipleMaterials = true;
                if (materials.Length != grassBladeMeshes.Length) { System.Array.Resize(ref materials, grassBladeMeshes.Length); }
                this.materials = materials;
            }

        }

        public void SetShadowMode(UnityEngine.Rendering.ShadowCastingMode mode) { shadowMode = mode; }
        public void SetRenderLayer(int layer) { renderLayer.Set(layer); }
        public void SetRenderLayer(SingleLayer layer) { renderLayer = layer; }

        public void SetPointCount(float count, bool multiplyByArea = false) {
            pointCount = count;
            this.multiplyByArea = multiplyByArea;
        }
        public void SetPointLODFactor(float value) { pointLODFactor = Mathf.Clamp01(value); }

        public void SetSeed(int seed) { randomiseSeed = false; this.seed = seed; }
        public void SetSeed(bool randomise) { randomiseSeed = randomise; }

        public void SetOverwriteNormal(Vector3 normal) {
            overwriteNormalDirection = true;
            forcedNormal = normal.normalized;
        }
        public void SetOverwriteNormal(bool enabled) { overwriteNormalDirection = enabled; }

        public void SetDensity(bool enabled, float cutoff = 0.5f) {
            useDensity = enabled;
            densityCutoff = cutoff;
        }
        public void SetLength(bool enabled, float rangeMin = 0.25f, float rangeMax = 1f) {
            useLength = enabled;
            lengthMapping = new Vector2(rangeMin, rangeMax);
        }

        public void SetProjection(ProjectionType type, LayerMask mask) {
            projectType = type;
            projectMask = mask;
        }

        public void SetBoundingBoxOffset(Bounds bounds) { boundingBoxOffset = bounds; }
        #endregion


        /// <summary>Gets debug info about the renderer (Point count, buffer sizes, etc.)</summary>
        /// <returns><c>DebugInformation</c> - The debug information</returns>
        public DebugInformation GetDebugInfo() {
            if (!enabled) {
                return new DebugInformation() {
                    totalPointCount = 0,
                    usingMultipleBuffers = false,
                    bufferCount = 0,
                    smallestBuffer = 0,
                    largestBuffer = 0
                };
            }

            DebugInformation info = new DebugInformation();
            info.usingMultipleBuffers = pointBuffers != null;
            if (info.usingMultipleBuffers) {
                int smallest = int.MaxValue;
                int largest = int.MinValue;
                for (int i = 0; i < pointBuffers.Length; i++) {
                    if (pointBuffers[i] != null && pointBuffers[i].IsValid()) {
                        int count = pointBuffers[i].count;
                        info.totalPointCount += pointBuffers[i].count;
                        if (count < smallest) { smallest = count; }
                        if (count > largest) { largest = count; }
                    }
                }
                info.bufferCount = pointBuffers.Length;
                info.smallestBuffer = smallest;
                info.largestBuffer = largest;
            }
            else if (pointBuffer != null && pointBuffer.IsValid()) {
                info.totalPointCount = pointBuffer.count;
                info.bufferCount = 1;
                info.smallestBuffer = info.totalPointCount;
                info.largestBuffer = info.totalPointCount;
            }

            return info;
        }
        /// <summary>A struct used for containing debug information about point grass renderers</summary>
        public struct DebugInformation {
            public int totalPointCount;
            public bool usingMultipleBuffers;
            public int bufferCount;
            public int smallestBuffer;
            public int largestBuffer;
        }
    }
}