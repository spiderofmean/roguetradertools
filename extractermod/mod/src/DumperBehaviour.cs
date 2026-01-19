using UnityEngine;

namespace BlueprintDumper
{
    public sealed class DumperBehaviour : MonoBehaviour
    {
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F10))
                Dumper.TriggerDump();
        }
    }
}
