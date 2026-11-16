using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Eto.WinUI.Drawing;

sealed class MatrixHandler : Matrix.IHandler
{
	static float DegreesToRadians(float degrees) => degrees * MathF.PI / 180f;

	public MatrixHandler()
	{
		ControlObject = Matrix3x2.Identity;
	}

	public MatrixHandler(Matrix3x2 matrix)
	{
		ControlObject = matrix;
	}

	public object ControlObject { get; private set; }

	Matrix3x2 Matrix
	{
		get => (Matrix3x2)ControlObject;
		set => ControlObject = value;
	}

	public float[] Elements => [Matrix.M11, Matrix.M21, Matrix.M12, Matrix.M22, Matrix.M31, Matrix.M32];

	public float Xx { get => Matrix.M11; set => SetMatrix(m => m.M11 = value); }
	public float Yx { get => Matrix.M21; set => SetMatrix(m => m.M21 = value); }
	public float Xy { get => Matrix.M12; set => SetMatrix(m => m.M12 = value); }
	public float Yy { get => Matrix.M22; set => SetMatrix(m => m.M22 = value); }
	public float X0 { get => Matrix.M31; set => SetMatrix(m => m.M31 = value); }
	public float Y0 { get => Matrix.M32; set => SetMatrix(m => m.M32 = value); }

	public void Create()
	{
		Matrix = Matrix3x2.Identity;
	}

	public void Create(float xx, float yx, float xy, float yy, float x0, float y0)
	{
		Matrix = new Matrix3x2(xx, xy, yx, yy, x0, y0);
	}

	public void Rotate(float angle)
	{
		Matrix = Matrix3x2.CreateRotation(DegreesToRadians(angle)) * Matrix;
	}

	public void RotateAt(float angle, float centerX, float centerY)
	{
		Matrix = Matrix3x2.CreateRotation(DegreesToRadians(angle), new Vector2(centerX, centerY)) * Matrix;
	}

	public void Translate(float offsetX, float offsetY)
	{
		Matrix = Matrix3x2.CreateTranslation(offsetX, offsetY) * Matrix;
	}

	public void Scale(float scaleX, float scaleY)
	{
		Matrix = Matrix3x2.CreateScale(scaleX, scaleY) * Matrix;
	}

	public void ScaleAt(float scaleX, float scaleY, float centerX, float centerY)
	{
		Matrix = Matrix3x2.CreateScale(scaleX, scaleY, new Vector2(centerX, centerY)) * Matrix;
	}

	public void Skew(float skewX, float skewY)
	{
		Matrix = Matrix3x2.CreateSkew(DegreesToRadians(skewX), DegreesToRadians(skewY)) * Matrix;
	}

	public void Append(IMatrix matrix)
	{
		if (matrix.ControlObject is Matrix3x2 other)
			Matrix *= other;
	}

	public void Prepend(IMatrix matrix)
	{
		if (matrix.ControlObject is Matrix3x2 other)
			Matrix = other * Matrix;
	}

	public void Invert()
	{
		if (!Matrix3x2.Invert(Matrix, out var inverted))
			throw new InvalidOperationException("Matrix is not invertible.");
		Matrix = inverted;
	}

	public PointF TransformPoint(Point point) => TransformPoint(new PointF(point.X, point.Y));

	public PointF TransformPoint(PointF point)
	{
		var transformed = Vector2.Transform(new Vector2(point.X, point.Y), Matrix);
		return new PointF(transformed.X, transformed.Y);
	}

	public IMatrix Clone() => new MatrixHandler(Matrix);

	public void Dispose()
	{
	}

	void SetMatrix(Action<Matrix3x2> updater)
	{
		var matrix = Matrix;
		updater(matrix);
		Matrix = matrix;
	}
}
