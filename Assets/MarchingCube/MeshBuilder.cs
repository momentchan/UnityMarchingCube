using UnityEngine;
using UnityEngine.Rendering;

namespace MarchingCubes
{
    public class MeshBuilder : System.IDisposable
    {
        Mesh _mesh;

        (int x, int y, int z) _grids;
        int _triangleBudget;
        ComputeShader _compute;

        public MeshBuilder(int x, int y, int z, int budget, ComputeShader compute)
            => Initialize((x, y, z), budget, compute);

        public MeshBuilder(Vector3Int dims, int budget, ComputeShader compute)
            => Initialize((dims.x, dims.y, dims.z), budget, compute);

        public void BuildIsosurface(ComputeBuffer voxels, float target, float scale)
            => RunCompute(voxels, target, scale);

        public Mesh Mesh => _mesh;

        void Initialize((int, int, int) dims, int budget, ComputeShader compute)
        {
            _grids = dims;
            _triangleBudget = budget;
            _compute = compute;

            AllocateBuffers();
            AllocateMesh(_triangleBudget * 3);
        }

        private void RunCompute(ComputeBuffer voxels, float target, float scale)
        {
            _counterBuffer.SetCounterValue(0);

            // Isosurface reconstruction
            _compute.SetInts("Dims", _grids);
            _compute.SetInt("MaxTriangle", _triangleBudget);
            _compute.SetFloat("Scale", scale);
            _compute.SetFloat("Isovalue", target);
            _compute.SetBuffer(0, "TriangleTable", _triangleTable);
            _compute.SetBuffer(0, "Voxels", voxels);
            _compute.SetBuffer(0, "VertexBuffer", _vertexBuffer);
            _compute.SetBuffer(0, "IndexBuffer", _indexBuffer);
            _compute.SetBuffer(0, "Counter", _counterBuffer);
            _compute.DispatchThreads(0, _grids);

            // Clear unused area of the buffers.
            _compute.SetBuffer(1, "VertexBuffer", _vertexBuffer);
            _compute.SetBuffer(1, "IndexBuffer", _indexBuffer);
            _compute.SetBuffer(1, "Counter", _counterBuffer);
            _compute.DispatchThreads(1, 1024, 1, 1);

            // Bounding box
            var ext = new Vector3(_grids.x, _grids.y, _grids.z) * scale;
            _mesh.bounds = new Bounds(Vector3.zero, ext);
        }


        #region Buffer
        ComputeBuffer _triangleTable;
        ComputeBuffer _counterBuffer;

        void AllocateBuffers()
        {
            _triangleTable = new ComputeBuffer(256, sizeof(ulong));
            _triangleTable.SetData(PrecalculatedData.TriangleTable);

            _counterBuffer = new ComputeBuffer(1, 4, ComputeBufferType.Counter);
        }

        void ReleaseBuffer()
        {
            _triangleTable.Dispose();
            _counterBuffer.Dispose();
        }

        #endregion

        #region Mesh

        GraphicsBuffer _vertexBuffer;
        GraphicsBuffer _indexBuffer;

        void AllocateMesh(int vertexCount)
        {
            _mesh = new Mesh();

            // Adding GraphicsBuffer.Target.Raw allows: read and write to the buffer in GPU compute shaders.
            _mesh.indexBufferTarget |= GraphicsBuffer.Target.Raw;
            _mesh.vertexBufferTarget |= GraphicsBuffer.Target.Raw;

            // position: float32 x 3
            var vp = new VertexAttributeDescriptor
                (VertexAttribute.Position, VertexAttributeFormat.Float32, 3);

            // normal: float32 x 3
            var vn = new VertexAttributeDescriptor
                (VertexAttribute.Normal, VertexAttributeFormat.Float32, 3);

            // vertex/index buffer formats
            _mesh.SetVertexBufferParams(vertexCount, vp, vn);
            _mesh.SetIndexBufferParams(vertexCount, IndexFormat.UInt32);

            // submesh initialization
            _mesh.SetSubMesh(0, new SubMeshDescriptor(0, vertexCount),
                MeshUpdateFlags.DontRecalculateBounds);

            // GraphicsBuffer references
            _vertexBuffer = _mesh.GetVertexBuffer(0);
            _indexBuffer = _mesh.GetIndexBuffer();
        }

        void ReleaseMesh()
        {
            _vertexBuffer.Dispose();
            _indexBuffer.Dispose();
            Object.Destroy(_mesh);
        }

        #endregion

        public void Dispose()
        {
            ReleaseBuffer();
            ReleaseMesh();
        }
    }
}