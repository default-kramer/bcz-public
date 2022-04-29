using FF2.Core;
using Godot;
using System;

using Color = FF2.Core.Color;

public class RootNode2D : Node2D
{
	// Declare member variables here. Examples:
	// private int a = 2;
	// private string b = "text";
	private Grid __grid;
	private Grid grid
	{
		get { return __grid; }
		set
		{
			__grid?.Dispose();
			__grid = value;
		}
	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		grid = Grid.Create(PRNG.Create());
	}

	public override void _Draw()
	{
		//Rotate((float)(frames * sign) / 5000);

		var rect = this.GetViewportRect();
		var cellX = rect.Size.x / grid.Width;
		var cellY = rect.Size.y / grid.Height;
		var screenCellSize = Math.Min(cellX, cellY);

		for (int x = 0; x < grid.Width; x++)
		{
			for (int y = 0; y < grid.Height; y++)
			{
				var loc = new Loc(x, y);
				var occ = grid.Get(loc);

				var canvasY = grid.Height - (y + 1);
				var screenY = canvasY * screenCellSize;
				var screenX = x * screenCellSize;

				switch (occ.Kind)
				{
					case OccupantKind.Catalyst:
						DrawCatalyst(occ, screenX, screenY, screenCellSize);
						break;
					case OccupantKind.Enemy:
						DrawEnemy(occ, screenX, screenY, screenCellSize);
						break;
				}
			}
		}
	}

	private static Godot.Color ConvertColor(Color color)
	{
		return color switch
		{
			Color.Red => Red,
			Color.Blue => Blue,
			Color.Yellow => Yellow,
			_ => throw new Exception("unexpected color: " + color),
		};
	}

	private readonly Vector2[] enemyPointBuffer = new Vector2[4];

	private void DrawEnemy(Occupant occ, float screenX, float screenY, float screenCellSize)
	{
		var gColor = ConvertColor(occ.Color);
		var half = screenCellSize / 2;
		enemyPointBuffer[0] = new Vector2(screenX + half, screenY);
		enemyPointBuffer[1] = new Vector2(screenX + screenCellSize, screenY + half);
		enemyPointBuffer[2] = new Vector2(screenX + half, screenY + screenCellSize);
		enemyPointBuffer[3] = new Vector2(screenX, screenY + half);
		DrawColoredPolygon(enemyPointBuffer, gColor);
	}

	private void DrawCatalyst(Occupant occ, float screenX, float screenY, float screenCellSize)
	{
		var gColor = ConvertColor(occ.Color);

		// always draw the circle
		var radius = screenCellSize / 2;
		var centerX = screenX + radius;
		var centerY = screenY + radius;
		DrawCircle(new Vector2(centerX, centerY), radius, gColor);

		// if we have a partner, draw the half-rectangle
		var half = screenCellSize / 2;
		Rect2 rect;

		switch (occ.Direction)
		{
			case Direction.Left:
				rect = new Rect2(screenX, screenY, half, screenCellSize);
				DrawRect(rect, gColor);
				break;
			case Direction.Right:
				rect = new Rect2(screenX + half, screenY, half, screenCellSize);
				DrawRect(rect, gColor);
				break;
			case Direction.Up:
				rect = new Rect2(screenX, screenY, screenCellSize, half);
				DrawRect(rect, gColor);
				break;
			case Direction.Down:
				rect = new Rect2(screenX, screenY + half, screenCellSize, half);
				DrawRect(rect, gColor);
				break;
		}
	}

	private static readonly Godot.Color Yellow = Godot.Colors.Yellow;
	private static readonly Godot.Color Red = Godot.Colors.Red;
	private static readonly Godot.Color Blue = Godot.Colors.Blue;

	//  // Called every frame. 'delta' is the elapsed time since the previous frame.
	int frames = 0;
	int sign = -1;
	public override void _Process(float delta)
	{
		frames++;
		if (frames >= 60)
		{
			sign = sign * -1;
			grid.Fall();
			frames = 0;
		}

		this.Update();
	}
}
