using System;
using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
class OverlapWFC : MonoBehaviour
{
	public Training training;
	public int gridsize = 1;
	[SerializeField] public int baseVisibleSize = 16; // camera visible (no always-on padding)
	public int seed = 0;
	public int N = 2;
	public bool periodicInput = false;
	public bool periodicOutput = false;
	public int symmetry = 1;
	public int foundation = 0;
	public int iterations = 0;
	public bool incremental = false;

	public OverlappingModel model;
	public GameObject[,] rendering;
	private Transform group;
	private bool undrawn = true;

	private Vector2Int windowOrigin;

	private int baseWidth;   // = baseVisibleSize + (N - 1)
	private int baseHeight;
	private bool extLeft, extRight, extUp, extDown;

	public int width;   // current model width (includes overlap band)
	public int depth;   // current model height (includes overlap band)

	private int VisibleWidth => Mathf.Max(0, width - (N - 1));
	private int VisibleHeight => Mathf.Max(0, depth - (N - 1));

	public void UpdateWindowOrigin(Vector2Int origin)
	{
		windowOrigin = origin;
	}

	void Awake()
	{
		EnsureGroup();
	}

	void EnsureGroup()
	{
		if (group != null) return;
		Transform root = transform.Find("output-overlap");
		if (root == null)
		{
			var go = new GameObject("output-overlap");
			go.transform.SetParent(transform);
			go.transform.localPosition = Vector3.zero;
			root = go.transform;
		}

		group = new GameObject("OverlapOutput").transform;
		group.SetParent(root);
		group.localPosition = Vector3.zero;
		group.localRotation = Quaternion.identity;
		group.localScale = Vector3.one;
	}

	public void GenerateInitialFullWindow(Vector2Int origin)
	{
		UpdateWindowOrigin(origin);
		Generate();
		Run();
	}

	public void Generate()
	{
		if (training == null)
		{
			Debug.LogError("OverlapWFC: No Training assigned.");
			return;
		}
		if (IsPrefabRef(training.gameObject))
		{
#if UNITY_EDITOR
			GameObject o = PrefabUtility.InstantiatePrefab(training.gameObject) as GameObject;
			o.transform.position = new Vector3(0, 99999f, 0f);
			training = o.GetComponent<Training>();
#else
            GameObject o = Instantiate(training.gameObject);
            o.transform.position = new Vector3(0, 99999f, 0f);
            training = o.GetComponent<Training>();
#endif
		}
		if (training.sample == null) training.Compile();

		ClearGroup();

		EnsureGroup();
		rendering = new GameObject[VisibleWidth, VisibleHeight];
		model = new OverlappingModel(training.sample, N, width, depth, periodicInput, periodicOutput, symmetry, foundation);
		undrawn = true;
	}

	public void Run()
	{
		if (model == null) return;
		if (!undrawn) return;

		if (model.Run(seed, iterations))
			Draw();
	}

	public void Draw()
	{
		if (group == null || model == null) return;
		undrawn = false;
		try
		{
			for (int y = 0; y < VisibleHeight; y++)
			{
				for (int x = 0; x < VisibleWidth; x++)
				{
					if (rendering[x, y] != null) continue;

					int sample = (int)model.Sample(x, y);
					if (sample == 99) { undrawn = true; continue; }

					if (sample >= 0 && sample < training.tiles.Length)
					{
						GameObject prefab = training.tiles[sample] as GameObject;
						if (prefab == null) continue;

						// Center tiles (adds 0.5 offset)
						Vector3 worldPos = new Vector3(
							(windowOrigin.x + x + 0.5f) * gridsize,
							(windowOrigin.y + y + 0.5f) * gridsize,
							0f);

						GameObject tile = Instantiate(prefab, worldPos, Quaternion.identity, group);

						if (training.RS != null && sample < training.RS.Length)
						{
							int rot = (int)training.RS[sample];
							tile.transform.localEulerAngles = new Vector3(0, 0, 360 - (rot * 90));
						}
						rendering[x, y] = tile;
					}
				}
			}
		}
		catch (IndexOutOfRangeException)
		{
			model = null;
		}
	}

	public GameObject GetTile(int localX, int localY)
	{
		if (rendering == null) return null;
		if (localX < 0 || localY < 0 || localX >= VisibleWidth || localY >= VisibleHeight) return null;
		return rendering[localX, localY];
	}

	public void RegenerateAt(Vector2Int newOrigin)
	{

		UpdateWindowOrigin(newOrigin);
		ClearGroup();
		EnsureGroup();
		rendering = new GameObject[VisibleWidth, VisibleHeight];

		model = new OverlappingModel(training.sample, N, width, depth, periodicInput, periodicOutput, symmetry, foundation);
		undrawn = true;
		Run();
	}

	void ClearGroup()
	{
		if (group != null)
		{
			if (Application.isPlaying) Destroy(group.gameObject);
			else DestroyImmediate(group.gameObject);
			group = null;
		}
	}

	// Slide the padded window by at most 1 tile per axis
	public void SlideWithConstraints(Vector2Int newPaddedOrigin)
	{
		// Old padded origin
		Vector2Int oldOrigin = windowOrigin;
		if (model == null)
		{
			// Fallback
			UpdateWindowOrigin(newPaddedOrigin);
			RegenerateAt(newPaddedOrigin);
			return;
		}

		// Compute overlap rectangle
		int oldMaxX = oldOrigin.x + VisibleWidth - 1;
		int oldMaxY = oldOrigin.y + VisibleHeight - 1;
		int newMaxX = newPaddedOrigin.x + VisibleWidth - 1;
		int newMaxY = newPaddedOrigin.y + VisibleHeight - 1;

		int overlapMinX = Mathf.Max(oldOrigin.x, newPaddedOrigin.x);
		int overlapMinY = Mathf.Max(oldOrigin.y, newPaddedOrigin.y);
		int overlapMaxX = Mathf.Min(oldMaxX, newMaxX);
		int overlapMaxY = Mathf.Min(oldMaxY, newMaxY);

		Dictionary<Vector2Int, byte> colorConstraints = new();

		if (overlapMinX <= overlapMaxX && overlapMinY <= overlapMaxY)
		{
			for (int wy = overlapMinY; wy <= overlapMaxY; wy++)
			{
				for (int wx = overlapMinX; wx <= overlapMaxX; wx++)
				{
					int localOldX = wx - oldOrigin.x;
					int localOldY = wy - oldOrigin.y;
					if (localOldX < 0 || localOldY < 0 || localOldX >= VisibleWidth || localOldY >= VisibleHeight) continue;
					int sample = model.Sample(localOldX, localOldY);
					if (sample == 99) continue; // unresolved
					colorConstraints[new Vector2Int(wx, wy)] = (byte)sample;
				}
			}
		}

		UpdateWindowOrigin(newPaddedOrigin);
		ClearGroup();
		EnsureGroup();
		rendering = new GameObject[VisibleWidth, VisibleHeight];
		model = new OverlappingModel(training.sample, N, width, depth, periodicInput, periodicOutput, symmetry, foundation);
		undrawn = true;


		if (colorConstraints.Count > 0)
		{
			foreach (var kv in colorConstraints)
			{
				int lx = kv.Key.x - newPaddedOrigin.x;
				int ly = kv.Key.y - newPaddedOrigin.y;
				if (lx < 0 || ly < 0 || lx >= VisibleWidth || ly >= VisibleHeight) continue;
				(model as OverlappingModel).ConstrainColor(lx, ly, kv.Value);
			}
		}

		bool ok = model.Run(seed, iterations);
		if (!ok)
		{
			// Fallback
			Debug.LogWarning("SlideWithConstraints: contradiction – regenerating without constraints.");
			RegenerateAt(newPaddedOrigin);
			return;
		}

		Draw();
	}

	public void SlideFromVisibleOrigins(Vector2Int oldVisibleOrigin, Vector2Int newVisibleOrigin, int padding)
	{
		Vector2Int newPadded = newVisibleOrigin - new Vector2Int(padding, padding);
		SlideWithConstraints(newPadded);
	}

	public void ConfigureBaseDimensions()
	{
		baseWidth = baseVisibleSize + (N - 1);
		baseHeight = baseVisibleSize + (N - 1);
		width = baseWidth;
		depth = baseHeight;
	}

	Dictionary<Vector2Int, byte> CaptureColors(int w, int h, Vector2Int origin)
	{
		var dict = new Dictionary<Vector2Int, byte>();
		if (model == null) return dict;
		for (int y = 0; y < VisibleHeight && y < h; y++)
		{
			for (int x = 0; x < VisibleWidth && x < w; x++)
			{
				int c = model.Sample(x, y);
				if (c == 99) continue;
				dict[new Vector2Int(origin.x + x, origin.y + y)] = (byte)c;
			}
		}
		return dict;
	}

	void ApplyColorConstraints(Dictionary<Vector2Int, byte> colors, Vector2Int newOrigin)
	{
		if (model == null || colors == null) return;
		foreach (var kv in colors)
		{
			int lx = kv.Key.x - newOrigin.x;
			int ly = kv.Key.y - newOrigin.y;
			if (lx < 0 || ly < 0 || lx >= VisibleWidth || ly >= VisibleHeight) continue;
			(model as OverlappingModel).ConstrainColor(lx, ly, kv.Value);
		}
	}

	// Rebuild model with current width/depth preserving overlap colors
	void RebuildModelPreserving(Dictionary<Vector2Int, byte> preserved, Vector2Int newOrigin)
	{
		UpdateWindowOrigin(newOrigin);
		ClearGroup();
		EnsureGroup();
		rendering = new GameObject[VisibleWidth, VisibleHeight];
		model = new OverlappingModel(training.sample, N, width, depth, periodicInput, periodicOutput, symmetry, foundation);
		undrawn = true;
		if (preserved != null && preserved.Count > 0)
			ApplyColorConstraints(preserved, newOrigin);
		model.Run(seed, iterations);
		Draw();
	}

	// Reset extensions (after committing a slide)
	void ResetExtensions()
	{
		if (extLeft || extRight || extUp || extDown)
		{
			extLeft = extRight = extUp = extDown = false;
			width = baseWidth;
			depth = baseHeight;
		}
	}

	private Vector2 currentMovementDir = Vector2.zero;

	void ResetExtension(bool isHorizontal, bool isPositive)
	{
		if (isHorizontal)
		{
			if (isPositive && extRight)
			{
				extRight = false;
				width = baseWidth;
			}
			else if (!isPositive && extLeft)
			{
				extLeft = false;
				width = baseWidth;
			}
		}
		else // vertical
		{
			if (isPositive && extUp)
			{
				extUp = false;
				depth = baseHeight;
			}
			else if (!isPositive && extDown)
			{
				extDown = false;
				depth = baseHeight;
			}
		}
	}

	public void EnsureDirectionalExtension(Vector2 movementDir)
	{
		if (model == null || movementDir.sqrMagnitude < 0.0001f) return;

		Vector2 prevDir = currentMovementDir;

		currentMovementDir = movementDir.normalized;

		if (movementDir.x > 0.2f && !extRight)
		{
			ExtendRight();
			Debug.Log($"Extended right: width={width}, VisibleWidth={VisibleWidth}");
		}

		if (movementDir.x < -0.2f && !extLeft)
		{
			ExtendLeft();
			Debug.Log($"Extended left: width={width}, VisibleWidth={VisibleWidth}");
		}

		if (movementDir.y > 0.2f && !extUp)
		{
			ExtendUp();
			Debug.Log($"Extended up: depth={depth}, VisibleHeight={VisibleHeight}");
		}

		if (movementDir.y < -0.2f && !extDown)
		{
			ExtendDown();
			Debug.Log($"Extended down: depth={depth}, VisibleHeight={VisibleHeight}");
		}

		// Only reset extensions when completely out of view
		Camera cam = Camera.main;
		if (cam != null)
		{
			Vector2 camPos = new Vector2(cam.transform.position.x, cam.transform.position.y);
			float visibleHalfWidth = baseVisibleSize * 0.5f;
			float visibleHalfHeight = baseVisibleSize * 0.5f;

			if (extRight && windowOrigin.x + VisibleWidth < camPos.x - visibleHalfWidth)
			{
				extRight = false;
				width = baseWidth + (extLeft ? 1 : 0);
				Debug.Log("Reset right extension - out of view");
			}

			if (extLeft && windowOrigin.x > camPos.x + visibleHalfWidth)
			{
				extLeft = false;
				width = baseWidth + (extRight ? 1 : 0);
				Debug.Log("Reset left extension - out of view");
			}

			if (extUp && windowOrigin.y + VisibleHeight < camPos.y - visibleHalfHeight)
			{
				extUp = false;
				depth = baseHeight + (extDown ? 1 : 0);
				Debug.Log("Reset up extension - out of view");
			}

			if (extDown && windowOrigin.y > camPos.y + visibleHalfHeight)
			{
				extDown = false;
				depth = baseHeight + (extUp ? 1 : 0);
				Debug.Log("Reset down extension - out of view");
			}
		}
	}

	public void SlideCommit(Vector2Int oldVisibleOrigin, Vector2Int newVisibleOrigin)
	{
		Vector2Int delta = newVisibleOrigin - oldVisibleOrigin;
		if (delta == Vector2Int.zero) return;

		var colors = CaptureColors(width, depth, windowOrigin);


		RebuildModelPreserving(colors, newVisibleOrigin);
	}

	void ExtendRight()
	{
		if (extRight) return;
		var colors = CaptureColors(width, depth, windowOrigin);
		width += 1;
		extRight = true;
		Debug.Log($"ExtendRight: width={width}, VisibleWidth={VisibleWidth}");

		RebuildModelPreserving(colors, windowOrigin);
	}

	void ExtendLeft()
	{
		if (extLeft) return;
		var colors = CaptureColors(width, depth, windowOrigin);
		width += 1;
		extLeft = true;
		Vector2Int newOrigin = new Vector2Int(windowOrigin.x - 1, windowOrigin.y);
		RebuildModelPreserving(colors, newOrigin);
	}

	void ExtendUp()
	{
		if (extUp) return;
		var colors = CaptureColors(width, depth, windowOrigin);
		depth += 1;
		extUp = true;
		RebuildModelPreserving(colors, windowOrigin);
	}

	void ExtendDown()
	{
		if (extDown) return;
		var colors = CaptureColors(width, depth, windowOrigin);
		depth += 1;
		extDown = true;
		Vector2Int newOrigin = new Vector2Int(windowOrigin.x, windowOrigin.y - 1);
		RebuildModelPreserving(colors, newOrigin);
	}

#if UNITY_EDITOR
	public static bool IsPrefabRef(UnityEngine.Object o)
	{
		return PrefabUtility.GetOutermostPrefabInstanceRoot(o) != null;
	}
#else
    public static bool IsPrefabRef(UnityEngine.Object o) => true;
#endif
}
