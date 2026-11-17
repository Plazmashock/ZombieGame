using UnityEngine;
using LastCheckout.Combat;
using LastCheckout.Interaction;
using LastCheckout.Inventory;
using LastCheckout.Items;

namespace LastCheckout.Game
{
    /// <summary>
    /// Supply crate in the Stockroom Annex. Once per loot phase (it "refills" every new wave):
    ///   + one pack of ammo for every gun you own (if it fits in your backpack)
    ///   + one medical item
    /// Own no guns and nothing fits? You get $30 instead, so it's never a wasted trip.
    /// </summary>
    public class SupplyCrate : MonoBehaviour, IInteractable
    {
        int claimedWave = -1;
        Transform lid;
        Light lamp;
        ItemDefinition[] meds;

        public void Setup(Transform lidTransform, Light statusLight)
        {
            lid = lidTransform;
            lamp = statusLight;
            // Every medical item that exists in the store (including the restocker's hidden copies).
            var list = new System.Collections.Generic.List<ItemDefinition>();
            foreach (var wi in LevelKit.FindAll<WorldItem>())
                if (wi.Item != null && wi.Item.category == ItemCategory.Medical && !list.Contains(wi.Item)) list.Add(wi.Item);
            meds = list.ToArray();
        }

        bool Ready => GameDirector.Instance != null && GameDirector.Instance.Wave != claimedWave;

        public string GetPrompt(PlayerInteractor who) => Ready ? "Open supply crate  ·  ammo + meds" : "Supply crate  ·  empty, refills next loot phase";
        public bool CanInteract(PlayerInteractor who) => Ready;

        public void Interact(PlayerInteractor who)
        {
            if (!Ready) return;
            claimedWave = GameDirector.Instance.Wave;

            var inventory = who.GetComponent<PlayerInventory>();
            var shooting = who.GetComponent<PlayerShooting>();
            var wallet = who.GetComponent<PlayerWallet>();
            int given = 0;

            if (inventory != null && shooting != null)
            {
                var seen = new System.Collections.Generic.HashSet<ItemDefinition>();
                foreach (var spec in WeaponCatalog.All)
                {
                    if (spec.IsMelee || spec.ammoItem == null || !shooting.Owns(spec) || !seen.Add(spec.ammoItem)) continue;
                    int fit = Mathf.Min(spec.ammoPack, inventory.HowManyFit(spec.ammoItem));
                    if (fit > 0 && inventory.TryAdd(spec.ammoItem, fit)) given++;
                }
                if (meds != null && meds.Length > 0)
                {
                    var med = meds[Random.Range(0, meds.Length)];
                    if (inventory.HowManyFit(med) > 0 && inventory.TryAdd(med, 1)) given++;
                }
            }
            if (given == 0 && wallet != null) wallet.Add(30, "SUPPLY CRATE");
        }

        void Update()
        {
            bool ready = Ready;
            if (lid != null)
            {
                Quaternion want = Quaternion.Euler(ready ? 0f : -70f, 0f, 0f);
                lid.localRotation = Quaternion.RotateTowards(lid.localRotation, want, 220f * Time.deltaTime);
            }
            if (lamp != null) lamp.color = ready ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.3f, 0.2f);
        }
    }
}
