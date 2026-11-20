using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CoinMultiplier
{
    // Same base class as the game's volume sliders, so it gets their highlight, keyboard and gamepad handling
    internal class CoinMultiplierMenuItem : Base_SliderMenuItem
    {
        internal const string ObjectName = "CoinMultiplierMenuItem";

        // One slider step is 0.1x
        private const float StepsPerUnit = 10f;

        // Distance from the item's center to the slider's left edge while the label is at normal size.
        // When the label grows on focus, the slider shifts right by the same amount its right edge moves.
        private const float SliderOffsetX = 25f;

        private RectTransform sliderRect;

        private bool rangeSet;

        // Base_SliderMenuItem.OnLeft/OnRight change Slider.value (which notifies) and then call
        // OnSliderValueChange again, so each press arrives twice. Tracking the applied step skips the repeat.
        private int appliedStep = -1;

        private int lastEnabledStep = ToStep(1.5f);

        private static int ToStep(float multiplier)
        {
            return Mathf.RoundToInt((multiplier - Plugin.MinMultiplier) * StepsPerUnit);
        }

        private static float FromStep(int step)
        {
            return Plugin.MinMultiplier + step / StepsPerUnit;
        }

        protected override void Awake()
        {
            base.Awake();
            DetachSliderFromLabel();
            // Changing the range can fire onValueChanged with a clamped value, so ignore changes until it is set
            Slider.wholeNumbers = true;
            Slider.minValue = 0f;
            Slider.maxValue = ToStep(Plugin.MaxMultiplier);
            rangeSet = true;
        }

        // In the copied volume item the slider is a child of Button/Text, so it moved whenever the label
        // changed width or scaled on focus. Move only the slider onto the item root; the label, its
        // highlight arrows and its mouse hit area (WideRaycastTarget) stay inside Text.
        private void DetachSliderFromLabel()
        {
            RectTransform rect = Slider.transform as RectTransform;
            if (rect == null)
            {
                Plugin.Log.LogWarning("Slider has no RectTransform, leaving it inline with the label");
                return;
            }

            Vector2 size = rect.sizeDelta;
            if (size.x <= 0f || size.y <= 0f)
            {
                size = rect.rect.size;
            }

            // Last sibling so it is drawn above the label's wide hit area and gets mouse drags first
            rect.SetParent(transform, false);
            rect.SetAsLastSibling();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(SliderOffsetX, 0f);
            rect.localScale = Vector3.one;
            sliderRect = rect;
        }

        private void LateUpdate()
        {
            // Clicking the slider makes it Unity's selected UI object, and Unity then also moves it on arrow
            // keys, on top of the game's own OnLeft/OnRight. Clearing the selection keeps one step per press
            // and does not interrupt a mouse drag.
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject == Slider.gameObject)
            {
                eventSystem.SetSelectedGameObject(null);
            }

            // The label scales around its pivot, so its right edge moves by (scale - 1) times its extent.
            // Only write the position when it changes, since each write marks the menu canvas for a rebuild.
            if (sliderRect != null && text != null)
            {
                float scale = text.rectTransform.localScale.x;
                float growth = scale == 1f ? 0f : Mathf.Max(0f, text.textBounds.max.x) * (scale - 1f);
                float x = SliderOffsetX + growth;
                if (sliderRect.anchoredPosition.x != x)
                {
                    sliderRect.anchoredPosition = new Vector2(x, 0f);
                }
            }
        }

        protected override void OnSliderValueChange()
        {
            int step = (int)Slider.value;
            if (!rangeSet || step == appliedStep)
            {
                return;
            }
            appliedStep = step;
            Plugin.Multiplier.Value = FromStep(step);
            if (step > 0)
            {
                lastEnabledStep = step;
            }
            RefreshLabel();
            SoundEffectsManager.Instance.Play("MenuItemSubmit");
        }

        // Submit toggles between x1 and the last value above it, like the volume sliders toggle mute
        public override void OnSubmit()
        {
            Slider.value = (Slider.value == 0f) ? lastEnabledStep : 0f;
            InteractionEffect();
        }

        public override void UpdateState()
        {
            int step = ToStep(Plugin.Multiplier.Value);
            if (step > 0)
            {
                lastEnabledStep = step;
            }
            appliedStep = step;
            Slider.SetValueWithoutNotify(step);
            RefreshLabel();
        }

        private void RefreshLabel()
        {
            if (text != null)
            {
                // Fixed decimals keep the label width constant while the value changes
                text.text = $"Coins x{Plugin.Multiplier.Value:0.00}";
            }
        }
    }

    internal static class MenuItemInjector
    {
        // Runs on every pause, so resolve the private field once instead of by name each time
        private static readonly AccessTools.FieldRef<PauseMenu, MenuPage> InitialMenuPage =
            AccessTools.FieldRefAccess<PauseMenu, MenuPage>("initialMenuPage");

        // Returns true when the pause menu has the in-game slider, false if the IMGUI fallback is needed
        internal static bool EnsureInjected(PauseMenu pauseMenu)
        {
            MenuPage page = InitialMenuPage(pauseMenu);
            Transform container = page != null ? page.transform.Find("Canvas/MenuItems") : null;
            if (container == null)
            {
                Plugin.Log.LogWarning("Pause menu item container not found, using overlay slider");
                return false;
            }
            if (container.Find(CoinMultiplierMenuItem.ObjectName) != null)
            {
                return true;
            }

            AudioVolumeMenuItem template = FindTemplate();
            if (template == null)
            {
                Plugin.Log.LogWarning("No volume slider found to copy, using overlay slider");
                return false;
            }

            // Build the copy under an inactive parent so no component runs Awake until it is set up
            GameObject holder = new GameObject("CoinMultiplierHolder");
            holder.SetActive(false);
            GameObject clone = Object.Instantiate(template.gameObject, holder.transform);
            clone.name = CoinMultiplierMenuItem.ObjectName;

            Object.DestroyImmediate(clone.GetComponent<AudioVolumeMenuItem>());
            foreach (Component component in clone.GetComponentsInChildren<Component>(true))
            {
                if (component != null && component.GetType().Name.Contains("Localize"))
                {
                    Object.DestroyImmediate(component);
                }
            }

            CoinMultiplierMenuItem item = clone.AddComponent<CoinMultiplierMenuItem>();
            // A serialized field on the copied component; OptionsMenuItem.Awake throws if it is null
            AccessTools.Field(typeof(OptionsMenuItem), "disableForPlatforms").SetValue(item, new Globals.Platform[0]);

            clone.transform.SetParent(container, false);
            clone.transform.SetSiblingIndex(Mathf.Min(1, container.childCount - 1));
            clone.SetActive(true);
            Object.Destroy(holder);

            Plugin.Log.LogInfo($"Added Coin Multiplier slider to the pause menu (copied from {template.name})");
            return true;
        }

        private static AudioVolumeMenuItem FindTemplate()
        {
            // Includes inactive objects, since the options menu is hidden while the pause menu is open
            AudioVolumeMenuItem[] candidates = Resources.FindObjectsOfTypeAll<AudioVolumeMenuItem>()
                .Where(c => c.gameObject.scene.IsValid())
                .ToArray();
            return candidates.FirstOrDefault(c => c is MasterVolumeMenuItem) ?? candidates.FirstOrDefault();
        }
    }
}
