using UnityEngine;

namespace LastCheckout.Combat
{
    /// <summary>
    /// The bow you SEE in your hand. Built by GunModelFactory.
    /// The string bends back and the arrow slides with it while you hold the mouse (PlayerShooting.DrawAmount).
    /// The nocked arrow disappears while you're out of arrows or nocking the next one.
    /// </summary>
    public class BowViewModel : MonoBehaviour
    {
        const float PullDistance = 0.3f; // meters the string comes back at full draw
        const float ArrowLength = 0.67f; // from nock to the start of the tip

        Transform model, stringTop, stringBottom, arrow;
        Vector3 topTip, bottomTip, nockRest;
        PlayerShooting shooting;
        CharacterController body;
        Transform cam;
        Vector3 restPosition;
        Quaternion restRotation;
        bool visible;
        float kick, bobTime, tuck;

        public void Configure(Transform modelRoot, Transform stringA, Transform stringB, Transform nockedArrow,
                              Vector3 topTipLocal, Vector3 bottomTipLocal, Vector3 nockRestLocal)
        {
            model = modelRoot;
            stringTop = stringA;
            stringBottom = stringB;
            arrow = nockedArrow;
            topTip = topTipLocal;
            bottomTip = bottomTipLocal;
            nockRest = nockRestLocal;
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            shooting = GetComponentInParent<PlayerShooting>();
            body = GetComponentInParent<CharacterController>();
            var c = GetComponentInParent<Camera>();
            cam = c != null ? c.transform : null;
            if (shooting != null) { shooting.Fired -= OnFired; shooting.Fired += OnFired; }
            SetVisible(false);
        }

        void OnDestroy() { if (shooting != null) shooting.Fired -= OnFired; }

        void OnFired(bool hit) { if (visible && shooting.CurrentWeaponId == "bow") kick = 1f; }

        void Update()
        {
            bool show = shooting != null && shooting.CurrentWeaponId == "bow";
            if (show != visible) SetVisible(show);
            if (!visible) return;

            float dt = Time.deltaTime;
            float draw = shooting.DrawAmount;
            kick = Mathf.Lerp(kick, 0f, 10f * dt);

            // String and arrow follow the draw.
            Vector3 nock = nockRest + Vector3.back * (PullDistance * draw);
            PlaceString(stringTop, topTip, nock);
            PlaceString(stringBottom, bottomTip, nock);
            bool arrowReady = shooting.Loaded > 0 && !shooting.IsReloading;
            if (arrow.gameObject.activeSelf != arrowReady) arrow.gameObject.SetActive(arrowReady);
            arrow.localPosition = nock + Vector3.forward * ArrowLength;

            float speed = 0f;
            if (body != null) { Vector3 v = body.velocity; v.y = 0f; speed = v.magnitude; }
            bobTime += speed * dt * 1.6f;
            float bob = Mathf.Clamp01(speed / 4f) * (1f - draw * 0.7f); // steadier while aiming
            Vector3 bobOffset = new Vector3(Mathf.Sin(bobTime) * 0.006f, -Mathf.Abs(Mathf.Sin(bobTime)) * 0.008f, 0f) * bob;

            tuck = Mathf.Lerp(tuck, ViewModelTuck.Target(cam), 1f - Mathf.Exp(-12f * dt));

            // While drawing, the bow eases a little towards the crosshair but stays on the right side of the screen.
            Vector3 position = restPosition + bobOffset + new Vector3(-0.08f * draw, 0.02f * draw, -0.03f * draw + 0.03f * kick) + ViewModelTuck.Offset(tuck);
            Quaternion rotation = ViewModelTuck.Turn(tuck) * restRotation * Quaternion.Euler(-4f * kick, 0f, 10f * draw);
            position += LastCheckout.Player.FirstPersonFeel.WeaponPos * (1f - draw * 0.8f); // steady while drawing
            rotation = Quaternion.Slerp(Quaternion.identity, LastCheckout.Player.FirstPersonFeel.WeaponRot, 1f - draw * 0.8f) * rotation;
            transform.SetLocalPositionAndRotation(position, rotation);
        }

        static void PlaceString(Transform s, Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            s.localPosition = (from + to) * 0.5f;
            s.localRotation = Quaternion.LookRotation(d.normalized, Vector3.right);
            s.localScale = new Vector3(0.004f, 0.004f, d.magnitude);
        }

        void SetVisible(bool value)
        {
            visible = value;
            if (model != null) model.gameObject.SetActive(value);
        }
    }
}
