using UnityEngine;

namespace MarchingCubes
{
    public class NoiseFieldVisualizer : MonoBehaviour
    {
        [SerializeField] Vector3Int _dimensions = new Vector3Int(64, 32, 64);
        [SerializeField] float _gridScale = 4.0f / 64f;
        [SerializeField] int _triangleBudget = 65536;
        [SerializeField] float _targetValue = 0f;

        [SerializeField] ComputeShader _volumeCompute = null;
        [SerializeField] ComputeShader _buildCompute = null;


        int VoxelCount => _dimensions.x * _dimensions.y * _dimensions.z;

        ComputeBuffer _voxelBuffer;
        MeshBuilder _builder;

        void Start()
        {
            _voxelBuffer = new ComputeBuffer(VoxelCount, sizeof(float));
            _builder = new MeshBuilder(_dimensions, _triangleBudget, _buildCompute);
        }

        void OnDestroy()
        {
            _voxelBuffer.Dispose();
            _builder.Dispose();
        }

        void Update()
        {
            // Noise field update
            _volumeCompute.SetInts("Dims", _dimensions);
            _volumeCompute.SetFloat("Scale", _gridScale);
            _volumeCompute.SetFloat("Time", Time.time);
            _volumeCompute.SetBuffer(0, "Voxels", _voxelBuffer);
            _volumeCompute.DispatchThreads(0, _dimensions);

            // Isosurface reconstruction
            _builder.BuildIsosurface(_voxelBuffer, _targetValue, _gridScale);
            GetComponent<MeshFilter>().sharedMesh = _builder.Mesh;
        }
    }
}