using UnityEngine;
using UnityEngine.UI;

// VR mod (Jen's son's request): Baldi's picture on the style picker, once
// Glitch style is unlocked, glitches while the cursor is on it - jumping a
// few pixels, flickering red and cyan and now and then stretching - and is
// still again when the cursor leaves. Pressing it plays Glitch style, as in
// the game.
public class GlitchOnHover : MonoBehaviour
{
	private StandardMenuButton button;
	private RectTransform rect;
	private Image image;
	private Vector2 restAt;
	private Vector3 restSize;
	private Color restColour;
	private bool glitching;
	private float nextJump;

	private void Awake()
	{
		button = GetComponent<StandardMenuButton>();
		rect = (RectTransform)transform;
		image = GetComponent<Image>();
		if (button != null)
		{
			button.eventOnHigh = true;
			button.OnHighlight.AddListener(() => Glitch(true));
			button.OffHighlight.AddListener(() => Glitch(false));
		}
	}

	private void Glitch(bool on)
	{
		if (on == glitching)
		{
			return;
		}
		glitching = on;
		if (on)
		{
			restAt = rect.anchoredPosition;
			restSize = rect.localScale;
			restColour = image != null ? image.color : Color.white;
			nextJump = 0f;
		}
		else
		{
			rect.anchoredPosition = restAt;
			rect.localScale = restSize;
			if (image != null)
			{
				image.color = restColour;
			}
		}
	}

	private void Update()
	{
		if (!glitching || Time.unscaledTime < nextJump)
		{
			return;
		}
		nextJump = Time.unscaledTime + Random.Range(0.03f, 0.09f);
		bool still = Random.value < 0.35f;
		rect.anchoredPosition = still ? restAt : restAt + new Vector2(Random.Range(-6, 7), Random.Range(-3, 4));
		rect.localScale = !still && Random.value < 0.25f ? new Vector3(restSize.x * Random.Range(1.05f, 1.3f), restSize.y * Random.Range(0.85f, 1f), restSize.z) : restSize;
		if (image != null)
		{
			float pick = Random.value;
			image.color = still ? restColour : pick < 0.33f ? new Color(1f, 0.35f, 0.35f) : pick < 0.66f ? new Color(0.35f, 1f, 1f) : restColour;
		}
	}

	private void OnDisable()
	{
		Glitch(false);
	}
}
