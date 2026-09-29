using System.Linq;
using UnityEngine;

namespace CoopPrototype.Frontend
{
    public sealed class MenuBackdrop : MonoBehaviour
    {
        public CharacterPreview[] partyPreviews;
        public void Present(GameFlow flow, IPlayerPreferences profile)
        {
            var members = flow.Members.Where(m=>m != null).OrderBy(m=>m.OwnerClientId).ToArray();
            for (int i=0;i<partyPreviews.Length;i++)
            {
                bool visible = members.Length == 0 ? i == 0 : i < members.Length;
                partyPreviews[i].gameObject.SetActive(visible);
                if (visible) partyPreviews[i].Show(members.Length == 0 ? profile.Character : members[i].Character.Value);
            }
        }
    }
}
