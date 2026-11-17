using System.Collections.Generic;
using UnityEngine;
using LastCheckout.Items;

namespace LastCheckout.Game
{
    /// <summary>
    /// Remembers every item lying in the store when the game starts (food, meds, tools, materials...),
    /// and puts back whatever was taken at the start of each new loot phase.
    /// So every round you can go shopping again, but you only have 60 seconds to carry it.
    /// </summary>
    public class SupplyRestocker : MonoBehaviour
    {
        class Entry { public WorldItem current; public GameObject mould; public Transform parent; public Vector3 pos; public Quaternion rot; }

        readonly List<Entry> entries = new List<Entry>();
        Transform moulds;

        /// <summary>Takes a snapshot of all loot in the store. Call once, before anything else spawns items.</summary>
        public void Snapshot()
        {
            moulds = new GameObject("Supply Moulds (do not touch)").transform;
            moulds.SetParent(transform, false);
            moulds.gameObject.SetActive(false); // copies under a switched-off parent never run or show

            foreach (var wi in LevelKit.FindAll<WorldItem>())
            {
                if (wi.Item == null || !wi.gameObject.activeInHierarchy) continue; // skip removed items (free ammo)
                entries.Add(new Entry
                {
                    current = wi,
                    mould = Instantiate(wi.gameObject, moulds),
                    parent = wi.transform.parent,
                    pos = wi.transform.position,
                    rot = wi.transform.rotation,
                });
            }
        }

        public int Restock()
        {
            int restocked = 0;
            foreach (var e in entries)
            {
                if (e.current != null) continue; // still on the shelf
                var go = Instantiate(e.mould, e.pos, e.rot, e.parent);
                go.name = e.mould.name.Replace("(Clone)", "").Trim();
                e.current = go.GetComponent<WorldItem>();
                restocked++;
            }
            return restocked;
        }
    }
}
