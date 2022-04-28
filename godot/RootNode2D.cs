using FF2.Core;
using Godot;
using System;
using System.Linq;

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
		grid = RAND.RandomGrid();
	}

	public override void _Draw()
	{
		Rotate((float)(frames * sign) / 5000);

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

				DrawOccupant(loc, occ, screenX, screenY, screenCellSize);
			}
		}
	}

	private void DrawOccupant(Loc loc, Occupant occ, float screenX, float screenY, float screenCellSize)
	{
		Godot.Color? gColor = occ.Color switch
		{
			Color.Red => Red,
			Color.Blue => Blue,
			Color.Yellow => Yellow,
			_ => null,
		};

		if (gColor.HasValue)
		{
			if (occ.Direction == Direction.None)
			{
				var radius = screenCellSize / 2;
				var centerX = screenX + radius;
				var centerY = screenY + radius;
				DrawCircle(new Vector2(centerX, centerY), radius, gColor.Value);
			}
			else
			{
				var rect = new Rect2(screenX, screenY, screenCellSize, screenCellSize);
				DrawRect(rect, gColor.Value);
			}
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

	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);
		grid = null;
	}
}
