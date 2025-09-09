using System.Collections;
using BepInEx;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PetDatDawg;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public sealed class PetDatDawg: BaseUnityPlugin {
    public const string HORNET_PET_ANIM_NAME = "Collect Stand 1";
    public const string PET_NPC_GO_NAME = "petGo";
    public const string PET_MARKER_GO_NAME = "petDialog";

    public const string BELL_BEAST_FSM_NAME = "Interaction";
    public const string BELL_BEAST_SHAKE_STATE_NAME = "Shake";
    public const string BELL_BEAST_GO_NAME = "Bone Beast NPC";

    public const float PET_DURATION = 2.0f;
    public const float INTERACTION_COLLIDER_HEIGHT = 10.0f;
    public const float INTERACTION_COLLIDER_WIDTH = INTERACTION_COLLIDER_HEIGHT / 2;

    private void Awake() {
        SceneManager.sceneLoaded += SceneLoaded;
    }

    private void SceneLoaded(Scene scene, LoadSceneMode mode) {
        var roots = scene.GetRootGameObjects();
        GameObject? dawg;
        if ((dawg = roots.FirstOrDefault(static x => x.name == BELL_BEAST_GO_NAME)) is null) {
            if ((dawg = roots.Select(static x => x.transform.Find(BELL_BEAST_GO_NAME)).FirstOrDefault(static x => x != null)?.gameObject) is null) {
                return;
            }
        }

        var go = new GameObject(PET_NPC_GO_NAME, typeof(PlayMakerNPC), typeof(BoxCollider2D));
        go.SetActive(dawg.activeSelf);
        go.transform.SetParent(dawg.transform);
        go.transform.localPosition = Vector2.zero;

        var markerObj = new GameObject(PET_MARKER_GO_NAME, typeof(PromptMarker));
        markerObj.SetActive(dawg.activeSelf);
        markerObj.transform.SetParent(go.transform);
        markerObj.transform.localPosition = Vector2.zero;

        var col = go.GetComponent<BoxCollider2D>();
        col.size = new(INTERACTION_COLLIDER_WIDTH, INTERACTION_COLLIDER_HEIGHT);
        col.isTrigger = true;

        var npc = go.GetComponent<PlayMakerNPC>();
        npc.PromptMarker = markerObj.transform;
        npc.InteractLabel = InteractableBase.PromptLabels.Inspect;
        npc.TalkPosition = NPCControlBase.TalkPositions.Any;
        // I hope unhook happens when npc is destroyed due to scene change
        npc.StartedDialogue += () => {
            StartCoroutine(HornetPet(npc, dawg));
        };
    }

    private IEnumerator HornetPet(PlayMakerNPC npc, GameObject dawg) {
        if (npc == null || dawg == null)
            yield break;

        var animator = HeroController.instance.gameObject.GetComponent<HeroAnimationController>();
        if (animator == null) {
            Logger.LogError($"Missing {nameof(HeroAnimationController)} on hornet GO");
            yield break;
        }

        try {
            animator.StopControl(); // avoid animator messing with animation
            animator.PlayClipForced(HORNET_PET_ANIM_NAME); // forced to bypass lack of control
            yield return new WaitForSeconds(animator.GetCurrentClipDuration());
            DawgGrumble(dawg);
            yield return new WaitForSeconds(PET_DURATION);
        }
        finally {
            // hopefully this can revert everything
            animator.StartControl();
            npc.EndDialogue();
            HeroController.instance.RegainControl(true);
        }
    }

    private void DawgGrumble(GameObject dawg) {
        var fsm = dawg.GetComponents<PlayMakerFSM>()
            .FirstOrDefault(x => x.FsmName == BELL_BEAST_FSM_NAME);
        if (fsm == null) {
            Logger.LogWarning($"No {nameof(PlayMakerFSM)} with name {BELL_BEAST_FSM_NAME} on dawg GO");
            return; // rumble is not vital, continue if we can
        }

        fsm.SetState(BELL_BEAST_SHAKE_STATE_NAME);
    }

    private void OnDestroy() {
        SceneManager.sceneLoaded -= SceneLoaded;
    }
}