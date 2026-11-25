using UnityEngine;

namespace SignalLost
{
    /// <summary>
    /// Turns pointer input into world actions. LEFT-CLICK pops the Static under the cursor. RIGHT-CLICK
    /// sucks: hold the right button to vacuum loose bits (Hold mode), then let go over a chute to sort them;
    /// or in Toggle mode right-click to start and right-click again to drop. Clicks use Camera.ScreenPointToRay
    /// + a sphere cast against blob colliders; the cursor's world position is the ray intersected with the
    /// play plane (y = 1.5). World clicks are ignored while the pointer is over UI or the game is paused.
    /// Behind the main menu Static can still be popped for fun (no vacuum, nothing drops).
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        private readonly RaycastHit[] hits = new RaycastHit[24];
        private Blob hovered;
        private bool suckHeld;
        private Vector3 lastPlanePoint;

        private void Update()
        {
            GameManager gm = GameManager.I;
            InputReader input = InputReader.I;
            if (gm == null || !gm.IsReady || input == null) return;
            Camera cam = gm.Cam;
            if (cam == null) return;

            bool overUI = input.IsPointerOverUI();
            Ray ray = cam.ScreenPointToRay(input.PointerPosition);
            if (gm.PlayPlane.Raycast(ray, out float d)) lastPlanePoint = ray.GetPoint(d);

            bool menu = gm.State == GameState.MainMenu;
            bool playing = gm.State == GameState.Playing && !gm.IsPaused && (DayManager.I == null || DayManager.I.InShiftOrLastCall);
            bool worldEnabled = (playing || menu) && (CameraDirector.I == null || !CameraDirector.I.InTransition || playing);
            if (Vacuum.I != null) Vacuum.I.SetCursor(lastPlanePoint, !overUI && playing);

            Physics.SyncTransforms();
            Blob under = worldEnabled && !overUI ? Pick(ray) : null;
            if (under != hovered)
            {
                if (hovered != null) hovered.Hover = 0f;
                hovered = under;
                if (hovered != null) hovered.Hover = 1f;
            }

            if (!worldEnabled)
            {
                if (Vacuum.I != null && Vacuum.I.Active && !gm.IsPaused) Vacuum.I.DropAll();
                suckHeld = false;
                return;
            }

            // LEFT-CLICK: pop Static (also works behind the main menu).
            if (input.PressedThisFrame && !overUI && under != null) BlobActions.Click(under);
            if (menu) return;

            Vacuum vac = Vacuum.I;
            if (vac == null) return;
            bool toggle = GameSettings.VacuumToggle;

            // RIGHT-CLICK: suck.
            if (input.SecondaryPressedThisFrame && !overUI)
            {
                if (toggle && vac.Active)
                {
                    // Toggle mode: the second right-click drops the tank.
                    vac.End(lastPlanePoint, true);
                    suckHeld = false;
                }
                else
                {
                    vac.Begin(lastPlanePoint);
                    suckHeld = true;
                }
            }

            if (toggle)
            {
                if (vac.Active) vac.Tick(lastPlanePoint);
                return;
            }

            if (suckHeld && input.SecondaryIsPressed)
            {
                if (vac.Active) vac.Tick(lastPlanePoint);
            }
            if (suckHeld && (input.SecondaryReleasedThisFrame || !input.SecondaryIsPressed))
            {
                suckHeld = false;
                if (vac.Active) vac.End(lastPlanePoint, true);
            }
        }

        private Blob Pick(Ray ray)
        {
            float radius = Balance.B != null ? Balance.B.click.pickRadius : 0.12f;
            int mask = 1 << BlobSpawner.BlobLayer;
            int n = Physics.SphereCastNonAlloc(ray, radius, hits, 200f, mask, QueryTriggerInteraction.Ignore);
            Blob best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                Blob b = hits[i].collider != null ? hits[i].collider.GetComponentInParent<Blob>() : null;
                if (b == null || !b.IsPoppable) continue;
                if (hits[i].distance < bestDist)
                {
                    bestDist = hits[i].distance;
                    best = b;
                }
            }
            return best;
        }
    }
}
