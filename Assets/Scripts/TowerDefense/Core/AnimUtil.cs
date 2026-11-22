using System.Collections.Generic;
using UnityEngine;

namespace ARTD
{
    /// <summary>Animator に該当パラメータがあるときだけ値を送る（仮モデルで Animator が無くてもエラーにしない）。</summary>
    public static class AnimUtil
    {
        static readonly Dictionary<Animator, HashSet<int>> cache = new Dictionary<Animator, HashSet<int>>();

        static bool Has(Animator a, string name, out int hash)
        {
            hash = 0;
            if (!a || string.IsNullOrEmpty(name) || !a.isActiveAndEnabled || !a.runtimeAnimatorController) return false;
            if (!cache.TryGetValue(a, out var set))
            {
                set = new HashSet<int>();
                foreach (var p in a.parameters) set.Add(p.nameHash);
                cache[a] = set;
            }
            hash = Animator.StringToHash(name);
            return set.Contains(hash);
        }

        public static void Trigger(Animator a, string name)
        {
            if (Has(a, name, out int h)) a.SetTrigger(h);
        }

        public static void Float(Animator a, string name, float v)
        {
            if (Has(a, name, out int h)) a.SetFloat(h, v);
        }

        public static void Bool(Animator a, string name, bool v)
        {
            if (Has(a, name, out int h)) a.SetBool(h, v);
        }
    }
}
