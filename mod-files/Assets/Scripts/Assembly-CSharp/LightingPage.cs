using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// The Lighting pages in the settings (Jen's son's request), after Graphics:
// Dynamic (the lit-up school: lamp pools, window-lit walls, depth),
// Filmmaker mode (moodier and cinematic), Normal (off: the game's own
// light, nothing added), and on Lighting 2, Plus style (the clean, bright,
// simple look of Baldi's Basics Plus). One is on at a time, and it is
// remembered. The school's shaders read it as _BaldiLightMode
// (Shader/BaldiCommon.cginc). Each page is the Graphics page copied, with its
// own title and ticks.
public class LightingPage : MonoBehaviour
{
	private const string Saved = "BaldiVRLighting";

	private static readonly string[] Names = { "Normal (off)", "Dynamic", "Filmmaker mode", "Snow day", "Halloween", "Christmas", "Space", "Alarm", "Candle", "Hospital", "Sunrise", "Infrared", "Comic", "Ghost", "Lava", "Arcade CRT", "Eclipse", "Plus style" };

	// Each page's ticks top to bottom (-1: that row is not used).
	// Each page's ticks top to bottom (-1: that row is not used). Jen's son
	// kept Dynamic, Filmmaker mode and Normal, and added Plus style.
	private static readonly int[][] Pages =
	{
		new[] { 1, 2, 0 },
		new[] { 17, -1, -1 },
	};

	private int[] Order;

	public static int Mode { get; private set; } = 1;

	private MenuToggle[] toggles;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Install()
	{
		Mode = PlayerPrefs.GetInt(Saved, 1);
		if (Mode != 0 && Mode != 2 && Mode != 17) Mode = 1;
		Shader.SetGlobalFloat("_BaldiLightMode", Mode);
		SceneManager.sceneLoaded += (scene, mode) => AddPages();
		AddPages();
	}

	private static void SetMode(int mode)
	{
		Mode = mode;
		Shader.SetGlobalFloat("_BaldiLightMode", mode);
		PlayerPrefs.SetInt(Saved, mode);
		PlayerPrefs.Save();
	}

	private static void AddPages()
	{
		foreach (OptionsMenu options in Object.FindObjectsOfType<OptionsMenu>(true))
		{
			if (options.GetComponentInChildren<LightingPage>(true) == null) AddPage(options);
		}
	}

	private static string PageName(int page)
	{
		return page == 0 ? "Lighting" : "Lighting " + (page + 1);
	}

	private static void AddPage(OptionsMenu options)
	{
		FieldInfo field = typeof(OptionsMenu).GetField("categories", BindingFlags.NonPublic | BindingFlags.Instance);
		GameObject[] categories = (GameObject[])field.GetValue(options);
		int graphics = System.Array.FindIndex(categories, c => c != null && c.name == "Graphics");
		if (graphics < 0) return;
		GameObject source = categories[graphics];
		GameObject after = graphics + 1 < categories.Length ? categories[graphics + 1] : categories[0];
		System.Collections.Generic.List<GameObject> list = new System.Collections.Generic.List<GameObject>(categories);
		for (int number = 0; number < Pages.Length; number++)
		{
			GameObject page = Object.Instantiate(source, source.transform.parent, false);
			page.name = PageName(number);
			page.SetActive(false);
			// Only the titles and the three ticks stay.
			foreach (Transform part in page.transform)
			{
				string name = part.name;
				if (name != "Title" && name != "PreviousTitle" && name != "NextTitle" && !name.EndsWith("Toggle")) Object.Destroy(part.gameObject);
			}
			SetText(page.transform.Find("Title"), PageName(number));
			SetText(page.transform.Find("PreviousTitle"), number == 0 ? Title(source) : PageName(number - 1));
			SetText(page.transform.Find("NextTitle"), number == Pages.Length - 1 ? Title(after) : PageName(number + 1));

			LightingPage lighting = page.AddComponent<LightingPage>();
			lighting.Order = Pages[number];
			lighting.toggles = new MenuToggle[3];
			string[] rows = { "FullScreenToggle", "VsyncToggle", "PixelFilterToggle" };
			for (int i = 0; i < 3; i++)
			{
				Transform row = page.transform.Find(rows[i]);
				if (row == null) continue;
				if (lighting.Order[i] < 0)
				{
					Object.Destroy(row.gameObject);
					continue;
				}
				row.name = Names[lighting.Order[i]];
				// Its tick no longer does the Graphics setting it was copied from.
				StandardMenuButton button = row.GetComponentInChildren<StandardMenuButton>(true);
				if (button != null)
				{
					button.OnPress = new UnityEngine.Events.UnityEvent();
					MenuToggle toggle = row.GetComponent<MenuToggle>();
					button.OnPress.AddListener(toggle.Toggle);
				}
				SetText(row.Find("ToggleText"), Names[lighting.Order[i]]);
				lighting.toggles[i] = row.GetComponent<MenuToggle>();
			}
			list.Insert(graphics + 1 + number, page);
		}
		// The pages either side now point at them.
		SetText(source.transform.Find("NextTitle"), PageName(0));
		SetText(after.transform.Find("PreviousTitle"), PageName(Pages.Length - 1));
		field.SetValue(options, list.ToArray());
	}

	private static string Title(GameObject category)
	{
		Transform title = category != null ? category.transform.Find("Title") : null;
		TMP_Text text = title != null ? title.GetComponentInChildren<TMP_Text>(true) : null;
		return text != null ? text.text : "";
	}

	// Its own words, so the game's translations don't put the old ones back.
	private static void SetText(Transform holder, string words)
	{
		if (holder == null) return;
		foreach (MonoBehaviour behaviour in holder.GetComponentsInChildren<MonoBehaviour>(true))
		{
			if (behaviour != null && behaviour.GetType().Name.Contains("Locali")) Object.Destroy(behaviour);
		}
		TMP_Text text = holder.GetComponentInChildren<TMP_Text>(true);
		if (text != null) text.text = words;
	}

	private void OnEnable()
	{
		Show();
	}

	// One tick on at a time: ticking another picks it; unticking the one
	// that is on leaves it on.
	private void Update()
	{
		if (toggles == null) return;
		for (int i = 0; i < toggles.Length; i++)
		{
			if (toggles[i] == null) continue;
			bool picked = Order[i] == Mode;
			if (toggles[i].Value && !picked)
			{
				SetMode(Order[i]);
				Show();
				return;
			}
			if (!toggles[i].Value && picked) toggles[i].Set(true);
		}
	}

	private void Show()
	{
		if (toggles == null) return;
		for (int i = 0; i < toggles.Length; i++)
		{
			if (toggles[i] != null) toggles[i].Set(Order[i] == Mode);
		}
	}
}
