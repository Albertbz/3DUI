using Meta.XR;
using Meta.XR.MRUtilityKit;
using UnityEngine;

namespace SpatialPhotos
{
    /// <summary>
    /// Finds a flat real surface (wall, table, floor…) near a photo and the pose that lays the photo flat on it.
    /// It uses the room's scene model when the scene has an <see cref="MRUK"/> and the room is scanned, and falls
    /// back to the environment depth raycast that <see cref="PhotoCapture"/> sets up.
    /// </summary>
    public static class SurfaceSnap
    {
        private const float MinNormalConfidence = 0.5f;
        private const float WallMaxUp = 0.7f; // |normal · up| below this is a wall, above it a table or floor

        private static EnvironmentRaycastManager raycastManager;
        private static bool sceneLoadRequested;

        /// <summary>
        /// Loads the room's scene model if the scene has an MRUK that doesn't load at startup. It loads without
        /// asking for a room scan, so a person without one isn't sent to Space Setup; snapping then uses depth only.
        /// </summary>
        public static void LoadScene()
        {
            MRUK mruk = MRUK.Instance;
            if (sceneLoadRequested || mruk == null || mruk.IsInitialized || mruk.SceneSettings.LoadSceneOnStartup)
            {
                return;
            }
    #if UNITY_ANDROID && !UNITY_EDITOR
            // PhotoCapture requests the permission; try again on a later call until it's granted
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(OVRPermissionsRequester.ScenePermission))
            {
                return;
            }
    #endif
            sceneLoadRequested = true;
            _ = mruk.LoadSceneFromDevice(requestSceneCaptureIfNoDataFound: false);
        }

        /// <summary>
        /// Finds the nearest surface within <paramref name="maxDistance"/> of <paramref name="position"/>.
        /// <paramref name="back"/> is the direction the photo's back faces, used for the depth raycast.
        /// The returned normal points out of the surface, toward the photo.
        /// </summary>
        public static bool TryFind(Vector3 position, Vector3 back, float maxDistance, out Vector3 point, out Vector3 normal)
        {
            LoadScene();
            return TryFindInRoom(position, maxDistance, out point, out normal)
                || TryFindWithDepth(position, back, maxDistance, out point, out normal);
        }

        /// <summary>The pose that lays a photo flat on the surface, its picture facing out and <paramref name="gap"/> in front of it.</summary>
        public static Pose PoseOnSurface(Vector3 point, Vector3 normal, Quaternion current, Vector3 viewer, float gap)
        {
            Vector3 up;
            if (Mathf.Abs(Vector3.Dot(normal, Vector3.up)) < WallMaxUp)
            {
                // Pictures on a wall hang straight
                up = Vector3.ProjectOnPlane(Vector3.up, normal);
            }
            else
            {
                // On a table or floor the photo keeps the turn it was held at, or else reads upright from the viewer
                up = Vector3.ProjectOnPlane(current * Vector3.up, normal);
                if (up.sqrMagnitude < 0.01f)
                {
                    up = Vector3.ProjectOnPlane(point - viewer, normal);
                }
                if (up.sqrMagnitude < 0.0001f)
                {
                    up = Vector3.ProjectOnPlane(Vector3.forward, normal);
                }
            }

            // The photo's +z is its back, so it faces the surface
            return new Pose(point + normal * gap, Quaternion.LookRotation(-normal, up.normalized));
        }

        private static bool TryFindInRoom(Vector3 position, float maxDistance, out Vector3 point, out Vector3 normal)
        {
            point = normal = Vector3.zero;
            MRUK mruk = MRUK.Instance;
            MRUKRoom room = mruk != null && mruk.IsInitialized ? mruk.GetCurrentRoom() : null;
            if (room == null)
            {
                return false;
            }

            // Negative inside a volume such as a table, which then snaps the photo to the nearest face
            float distance = room.TryGetClosestSurfacePosition(position, out point, out _, out normal);
            if (Mathf.Abs(distance) > maxDistance || normal.sqrMagnitude < 0.5f)
            {
                return false;
            }
            if (distance > 0f && Vector3.Dot(normal, position - point) < 0f)
            {
                normal = -normal; // A plane's normal may face away from the photo
            }
            normal.Normalize();
            return true;
        }

        private static bool TryFindWithDepth(Vector3 position, Vector3 back, float maxDistance, out Vector3 point, out Vector3 normal)
        {
            point = normal = Vector3.zero;
            if (raycastManager == null)
            {
                raycastManager = Object.FindFirstObjectByType<EnvironmentRaycastManager>();
            }
            if (raycastManager == null || !raycastManager.isActiveAndEnabled)
            {
                return false;
            }

            // Behind the photo (walls) and below it (tables, floor); the nearer confident hit wins
            float best = float.PositiveInfinity;
            DepthRaycast(position, back, maxDistance, ref best, ref point, ref normal);
            DepthRaycast(position, Vector3.down, maxDistance, ref best, ref point, ref normal);
            normal.Normalize();
            return best <= maxDistance;
        }

        private static void DepthRaycast(Vector3 position, Vector3 direction, float maxDistance, ref float best, ref Vector3 point, ref Vector3 normal)
        {
            if (!raycastManager.Raycast(new Ray(position, direction), out EnvironmentRaycastHit hit, maxDistance)
                || hit.normalConfidence < MinNormalConfidence)
            {
                return;
            }
            float distance = Vector3.Distance(position, hit.point);
            if (distance < best)
            {
                best = distance;
                point = hit.point;
                normal = Vector3.Dot(hit.normal, position - hit.point) < 0f ? -hit.normal : hit.normal;
            }
        }
    }
}
