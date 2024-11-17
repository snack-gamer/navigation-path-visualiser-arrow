using System.Collections.Generic;
using Managers;
using UnityEngine;
using UnityEngine.AI;

namespace Path_Guiding_System
{
    public class PathGuideSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LineRenderer pathLine;
        [SerializeField] private float lineHeight = 0.5f;
        [SerializeField] private float updateInterval = 0.2f;
        
        [Header("NavMesh Settings")]
        [SerializeField] private float navMeshSampleDistance = 10f;
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private bool allowPartialPath = true;
        
        [Header("Optimization")]
        [SerializeField] [Range(2, 5)] private int smoothingSubdivisions = 3;

        [Header("Material Animation")]
        [SerializeField] private float scrollSpeed = 1f;
        [SerializeField] private float positionLerpSpeed = 10f;
        
        [SerializeField] private Transform targetATransform;
        [SerializeField] private Transform targetBTransform;
        private NavMeshPath path;
        private float nextUpdateTime;
        private bool isInitialized;
        private float textureOffset;
        
        private readonly List<Vector3> smoothedPathCache = new List<Vector3>(100);
        private readonly List<Vector3> currentPositions = new List<Vector3>(100);
        private readonly List<Vector3> targetPositions = new List<Vector3>(100);
        [SerializeField] private Material lineMaterial;

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
            
            if (UpdateTransformReferences())
            {
                isInitialized = true;
                if (showDebugInfo) Debug.Log("PathGuideSystem initialized successfully");
            }
            
            lineMaterial = pathLine.material;
        }

        public void ForceUpdateTransforms()
        {
            if (UpdateTransformReferences())
            {
                CalculatePath();
                nextUpdateTime = Time.time + updateInterval;
        
                if (showDebugInfo) Debug.Log("Transforms forcefully updated");
            }
            else
            {
                if (showDebugInfo) Debug.LogWarning("Failed to force update transforms");
            }
        }
        
        private void SetupLineRenderer()
        {
            pathLine.startWidth = 0.2f;
            pathLine.endWidth = 0.2f;
            pathLine.useWorldSpace = true;
            pathLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pathLine.receiveShadows = false;
            
            // Initialize with 2 points minimum
            pathLine.positionCount = 2;
            pathLine.SetPosition(0, Vector3.zero);
            pathLine.SetPosition(1, Vector3.forward);
            
            pathLine.material = lineMaterial;
        }

        private void OnDestroy()
        {
            if (lineMaterial != null)
            {
                Destroy(lineMaterial);
            }
        }

        private void Update()
        {
            if (!isInitialized)
            {
                InitializeSystem();
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


        private void CalculatePath()
        {
            if (targetATransform == null || targetBTransform == null)
            {
                if (!UpdateTransformReferences()) return;
            }

            if (path == null)
            {
                path = new NavMeshPath();
                if (showDebugInfo) Debug.Log("Created new NavMeshPath");
            }

            bool startFound, endFound;
            Vector3 startPos = GetNavMeshPosition(targetATransform.position, out startFound);
            Vector3 endPos = GetNavMeshPosition(targetBTransform.position, out endFound);

            if (!IsValidPosition(startPos) || !IsValidPosition(endPos))
            {
                if (showDebugInfo) Debug.LogWarning("Invalid positions detected");
                targetPositions.Clear();
                return;
            }

            if (!NavMesh.CalculatePath(startPos, endPos, NavMesh.AllAreas, path))
            {
                if (showDebugInfo) Debug.LogWarning("Failed to calculate path");
                return;
            }

            ProcessPathResult();
        }
        
        private Vector3[] GetExtendedPath()
        {
            if (path.corners.Length == 0) return new Vector3[0];
            
            Vector3[] extendedPath = new Vector3[path.corners.Length + 1];
            path.corners.CopyTo(extendedPath, 0);
            extendedPath[extendedPath.Length - 1] = targetBTransform.position;
            return extendedPath;
        }
        
        private void ProcessPathResult()
        {
            switch (path.status)
            {
                case NavMeshPathStatus.PathComplete:
                    UpdateTargetPositions(path.corners, true);
                    break;
                    
                case NavMeshPathStatus.PathPartial:
                    if (allowPartialPath)
                    {
                        Vector3[] extendedPath = GetExtendedPath();
                        UpdateTargetPositions(extendedPath, false);
                    }
                    else
                    {
                        targetPositions.Clear();
                    }
                    break;
                    
                case NavMeshPathStatus.PathInvalid:
                    targetPositions.Clear();
                    if (showDebugInfo) Debug.LogWarning("Invalid path");
                    break;
            }
        }
        
        private void UpdateTargetPositions(Vector3[] pathPoints, bool isComplete)
        {
            if (pathPoints == null || pathPoints.Length < 2)
            {
                if (showDebugInfo) Debug.LogWarning("Invalid path points");
                return;
            }

            targetPositions.Clear();
            
            Vector3[] elevatedPoints = new Vector3[pathPoints.Length];
            for (int i = 0; i < pathPoints.Length; i++)
            {
                elevatedPoints[i] = pathPoints[i] + Vector3.up * lineHeight;
            }
            
            smoothedPathCache.Clear();
            SmoothPath(elevatedPoints, smoothedPathCache);
            targetPositions.AddRange(smoothedPathCache);

            if (showDebugInfo)
            {
                Debug.Log($"Target positions count: {targetPositions.Count}");
            }

            // Initialize current positions with target positions
            if (currentPositions.Count == 0)
            {
                currentPositions.AddRange(targetPositions);
                pathLine.positionCount = currentPositions.Count;
                pathLine.SetPositions(currentPositions.ToArray());
            }
        }
        
        private void UpdateMaterialScroll()
        {
            if (lineMaterial != null && pathLine.positionCount > 0)
            {
                textureOffset += Time.deltaTime * scrollSpeed;
                lineMaterial.mainTextureOffset = new Vector2(-textureOffset, 0f);
            }
        }
        
        private void UpdateLinePositions()
        {
            if (currentPositions.Count == 0 || targetPositions.Count == 0) return;

            bool needsUpdate = false;
            
            // Ensure lists have same size
            while (currentPositions.Count < targetPositions.Count)
                currentPositions.Add(targetPositions[currentPositions.Count]);
            while (currentPositions.Count > targetPositions.Count)
                currentPositions.RemoveAt(currentPositions.Count - 1);

            // Lerp positions
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
        
        // Rest of the methods remain the same as the working version...
        private bool UpdateTransformReferences()
        {
            try
            {
                var transforms = References.AreaManager.GetTransforms();
                
                if (transforms.Item1 == null || transforms.Item2 == null)
                {
                    if (showDebugInfo) Debug.LogError("One or both transforms are null from AreaManager");
                    return false;
                }
                
                targetATransform = transforms.Item1;
                targetBTransform = transforms.Item2;
                
                return true;
            }
            catch (System.Exception e)
            {
                if (showDebugInfo) Debug.LogError($"Error updating transforms: {e.Message}");
                return false;
            }
        }

        private Vector3 GetNavMeshPosition(Vector3 worldPosition, out bool found)
        {
            found = false;
            NavMeshHit hit;
            
            if (NavMesh.SamplePosition(worldPosition, out hit, navMeshSampleDistance, NavMesh.AllAreas))
            {
                found = true;
                return hit.position;
            }
            
            float[] heightOffsets = new float[] { 2f, 5f, -2f, -5f };
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

        private void UpdatePath()
        {
            if (targetATransform == null || targetBTransform == null)
            {
                if (!UpdateTransformReferences()) return;
            }

            bool startFound, endFound;
            Vector3 startPos = GetNavMeshPosition(targetATransform.position, out startFound);
            Vector3 endPos = GetNavMeshPosition(targetBTransform.position, out endFound);

            if (showDebugInfo)
            {
                Debug.DrawLine(targetATransform.position, startPos, Color.yellow, updateInterval);
                Debug.DrawLine(targetBTransform.position, endPos, Color.yellow, updateInterval);
                
                if (!startFound) Debug.LogWarning("Could not find valid NavMesh position for player");
                if (!endFound) Debug.LogWarning("Could not find valid NavMesh position for gate");
            }

            if (!IsValidPosition(startPos) || !IsValidPosition(endPos))
            {
                pathLine.positionCount = 0;
                return;
            }

            NavMesh.CalculatePath(startPos, endPos, NavMesh.AllAreas, path);

            switch (path.status)
            {
                case NavMeshPathStatus.PathComplete:
                    DrawPath(path.corners, Color.green);
                    break;
                    
                case NavMeshPathStatus.PathPartial:
                    if (allowPartialPath)
                    {
                        DrawPath(path.corners, Color.yellow);
                        
                        if (path.corners.Length > 0)
                        {
                            Vector3 lastNavMeshPoint = path.corners[path.corners.Length - 1];
                            Vector3[] extendedPath = new Vector3[path.corners.Length + 1];
                            path.corners.CopyTo(extendedPath, 0);
                            extendedPath[extendedPath.Length - 1] = endPos;
                            DrawPath(extendedPath, Color.yellow);
                        }
                    }
                    else
                    {
                        pathLine.positionCount = 0;
                    }
                    break;
                    
                case NavMeshPathStatus.PathInvalid:
                    pathLine.positionCount = 0;
                    if (showDebugInfo) Debug.LogWarning("Invalid path");
                    break;
            }
        }

        private bool IsValidPosition(Vector3 position)
        {
            return !float.IsInfinity(position.x) && 
                   !float.IsInfinity(position.y) && 
                   !float.IsInfinity(position.z);
        }

        private void DrawPath(Vector3[] pathPoints, Color pathColor)
        {
            if (pathPoints.Length < 2)
            {
                pathLine.positionCount = 0;
                return;
            }

            Vector3[] elevatedPoints = new Vector3[pathPoints.Length];
            for (int i = 0; i < pathPoints.Length; i++)
            {
                elevatedPoints[i] = pathPoints[i] + Vector3.up * lineHeight;
            }
            
            smoothedPathCache.Clear();
            SmoothPath(elevatedPoints, smoothedPathCache);
            
            pathLine.positionCount = smoothedPathCache.Count;
            pathLine.SetPositions(smoothedPathCache.ToArray());
            pathLine.startColor = pathColor;
            pathLine.endColor = pathColor;

            if (showDebugInfo)
            {
                for (int i = 0; i < pathPoints.Length - 1; i++)
                {
                    Debug.DrawLine(pathPoints[i], pathPoints[i + 1], pathColor, updateInterval);
                }
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
                    Vector3 interpolated = Vector3.Lerp(start, end, t);
                    smoothedPath.Add(interpolated);
                }
            }
            
            smoothedPath.Add(originalPath[originalPath.Length - 1]);
        }
    }
}