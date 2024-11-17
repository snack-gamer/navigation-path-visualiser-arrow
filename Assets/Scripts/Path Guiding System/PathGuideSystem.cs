using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Path_Guiding_System
{
    public class PathGuideSystem : MonoBehaviour
    {
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        [Header("References")]
        [SerializeField] private LineRenderer pathLine;
        [SerializeField] private float lineHeight = 0.5f;
        [SerializeField] private float updateInterval = 0.2f;
        
        [Header("NavMesh Settings")]
        [SerializeField] private float navMeshSampleDistance = 10f;
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private bool allowPartialPath = true;
        
        [Header("Optimization")]
        [SerializeField] [Range(2, 10)] private int smoothingSubdivisions = 3;

        [Header("Visuals")]
        [SerializeField] [Range(0.25f, 3)] private float scrollSpeed = 1f;
        [SerializeField] private float positionLerpSpeed = 10f;
        [SerializeField] [Range(0.25f, 2.5f)] private float pathWidth = 1;
        [SerializeField] private Color pathColor = Color.white;
        [SerializeField] [Range(0.05f, 2)] private float arrowTiles = 0.25f;
        
        
        [Header("Transform References")]
        [SerializeField] private Transform targetATransform;
        [SerializeField] private Transform targetBTransform;

        private NavMeshPath path;
        private float nextUpdateTime;
        private bool isInitialized;
        private float textureOffset;
        [SerializeField] private Material lineMaterial;
        
        private readonly List<Vector3> smoothedPathCache = new List<Vector3>(100);
        private readonly List<Vector3> currentPositions = new List<Vector3>(100);
        private readonly List<Vector3> targetPositions = new List<Vector3>(100);

        // Public properties to set transforms externally
        public Transform TargetA
        {
            get => targetATransform;
            set
            {
                targetATransform = value;
                CheckAndInitialize();
            }
        }

        public Transform TargetB
        {
            get => targetBTransform;
            set
            {
                targetBTransform = value;
                CheckAndInitialize();
            }
        }

        private void Start()
        {
            InitializeSystem();
            
#if UNITY_IOS || UNITY_ANDROID
            updateInterval = Mathf.Max(updateInterval, 0.3f);
            smoothingSubdivisions = Mathf.Min(smoothingSubdivisions, 3);
#endif
        }

        private void InitializeSystem()
        {
            path = new NavMeshPath();
            
            if (pathLine == null)
            {
                pathLine = gameObject.AddComponent<LineRenderer>();
                SetupLineRenderer();
            }
            
            CheckAndInitialize();
            lineMaterial = pathLine.material;
        }

        private void SetupLineRenderer()
        {
            pathLine.startWidth = 0.2f;
            pathLine.endWidth = 0.2f;
            pathLine.useWorldSpace = true;
            pathLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pathLine.receiveShadows = false;
            pathLine.positionCount = 2;
            pathLine.SetPosition(0, Vector3.zero);
            pathLine.SetPosition(1, Vector3.forward);
        }

        private void CheckAndInitialize()
        {
            if (targetATransform != null && targetBTransform != null)
            {
                isInitialized = true;
                if (showDebugInfo) Debug.Log($"PathGuideSystem initialized with transforms A: {targetATransform.name} and B: {targetBTransform.name}");
            }
            else
            {
                isInitialized = false;
                if (showDebugInfo) Debug.LogWarning("PathGuideSystem not initialized - missing transforms. Set via Inspector or SetTransforms()");
            }
        }

        public void SetTransforms(Transform transformA, Transform transformB)
        {
            if (transformA == null || transformB == null)
            {
                if (showDebugInfo) Debug.LogError("Cannot set null transforms");
                return;
            }

            targetATransform = transformA;
            targetBTransform = transformB;
            CheckAndInitialize();
            
            if (isInitialized)
            {
                CalculatePath();
                nextUpdateTime = Time.time + updateInterval;
            }
        }

        private void Update()
        {
            if (!isInitialized)
            {
                CheckAndInitialize();
                return;
            }

            if (Time.time >= nextUpdateTime)
            {
                CalculatePath();
                nextUpdateTime = Time.time + updateInterval;
            }

            UpdateLinePositions();
            UpdateMaterialScroll();
        }

        private Vector3 GetNavMeshPosition(Vector3 worldPosition, out bool found)
        {
            found = false;
            if (NavMesh.SamplePosition(worldPosition, out NavMeshHit hit, navMeshSampleDistance, NavMesh.AllAreas))
            {
                found = true;
                return hit.position;
            }
            
            float[] heightOffsets = { 2f, 5f, -2f, -5f };
            foreach (float offset in heightOffsets)
            {
                Vector3 offsetPosition = worldPosition + Vector3.up * offset;
                if (NavMesh.SamplePosition(offsetPosition, out hit, navMeshSampleDistance, NavMesh.AllAreas))
                {
                    found = true;
                    return hit.position;
                }
            }

            return new Vector3(worldPosition.x, 0f, worldPosition.z);
        }

        private void CalculatePath()
        {
            if (!NavMesh.SamplePosition(targetATransform.position, out NavMeshHit startHit, navMeshSampleDistance, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(targetBTransform.position, out NavMeshHit endHit, navMeshSampleDistance, NavMesh.AllAreas))
            {
                if (showDebugInfo) Debug.LogWarning("Could not find valid NavMesh positions");
                targetPositions.Clear();
                return;
            }

            if (NavMesh.CalculatePath(startHit.position, endHit.position, NavMesh.AllAreas, path))
            {
                ProcessPathResult();
            }
            else if (showDebugInfo)
            {
                Debug.LogWarning("Failed to calculate path");
            }
        }

        private void ProcessPathResult()
        {
            if (path.status == NavMeshPathStatus.PathComplete || (allowPartialPath && path.status == NavMeshPathStatus.PathPartial))
            {
                Vector3[] pathPoints = path.corners;
                if (path.status == NavMeshPathStatus.PathPartial)
                {
                    Vector3[] extendedPath = new Vector3[pathPoints.Length + 1];
                    pathPoints.CopyTo(extendedPath, 0);
                    extendedPath[^1] = targetBTransform.position;
                    pathPoints = extendedPath;
                }

                UpdateTargetPositions(pathPoints);
            }
            else
            {
                targetPositions.Clear();
                if (showDebugInfo) Debug.LogWarning("Invalid path");
            }
        }

        private void UpdateTargetPositions(Vector3[] pathPoints)
        {
            if (pathPoints == null || pathPoints.Length < 2) return;

            targetPositions.Clear();
            smoothedPathCache.Clear();

            // Elevate and smooth the path
            Vector3[] elevatedPoints = new Vector3[pathPoints.Length];
            for (int i = 0; i < pathPoints.Length; i++)
            {
                elevatedPoints[i] = pathPoints[i] + Vector3.up * lineHeight;
            }
            
            SmoothPath(elevatedPoints, smoothedPathCache);
            targetPositions.AddRange(smoothedPathCache);

            // Initialize current positions if needed
            if (currentPositions.Count == 0)
            {
                currentPositions.AddRange(targetPositions);
                pathLine.positionCount = currentPositions.Count;
                pathLine.SetPositions(currentPositions.ToArray());
            }
        }

        private void UpdateMaterialScroll()
        {
            var tiling = new Vector2(arrowTiles, 1);
            
            if (lineMaterial != null && pathLine.positionCount > 0)
            {
                textureOffset += Time.deltaTime * scrollSpeed;
                lineMaterial.mainTextureOffset = new Vector2(-textureOffset, 0f);
                pathLine.startWidth = pathWidth;
                pathLine.endWidth = pathWidth;
                lineMaterial.color = pathColor;
                lineMaterial.SetColor(EmissionColor, pathColor);


                lineMaterial.SetTextureScale(BaseMap, tiling);

            }
        }

        private void UpdateLinePositions()
        {
            if (currentPositions.Count == 0 || targetPositions.Count == 0) return;

            // Ensure lists have same size
            while (currentPositions.Count < targetPositions.Count)
                currentPositions.Add(targetPositions[currentPositions.Count]);
            while (currentPositions.Count > targetPositions.Count)
                currentPositions.RemoveAt(currentPositions.Count - 1);

            bool needsUpdate = false;
            for (int i = 0; i < currentPositions.Count; i++)
            {
                Vector3 current = currentPositions[i];
                Vector3 target = targetPositions[i];
                
                if ((current - target).sqrMagnitude > 0.001f)
                {
                    currentPositions[i] = Vector3.Lerp(current, target, Time.deltaTime * positionLerpSpeed);
                    needsUpdate = true;
                }
            }

            if (needsUpdate)
            {
                pathLine.positionCount = currentPositions.Count;
                pathLine.SetPositions(currentPositions.ToArray());
            }
        }

        private void SmoothPath(Vector3[] originalPath, List<Vector3> smoothedPath)
        {
            if (originalPath.Length < 2)
            {
                smoothedPath.AddRange(originalPath);
                return;
            }

            smoothedPath.Add(originalPath[0]);
            
            for (int i = 0; i < originalPath.Length - 1; i++)
            {
                Vector3 start = originalPath[i];
                Vector3 end = originalPath[i + 1];
                
                for (int j = 1; j < smoothingSubdivisions; j++)
                {
                    float t = j / (float)smoothingSubdivisions;
                    smoothedPath.Add(Vector3.Lerp(start, end, t));
                }
            }
            
            smoothedPath.Add(originalPath[^1]);
        }

        private void OnDestroy()
        {
            if (lineMaterial != null)
            {
                Destroy(lineMaterial);
            }
        }
    }
}