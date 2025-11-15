using System.IO;
using Unity.Collections;
using UnityEngine;

public class DepthBufferDumper : MonoBehaviour
{
	public Camera cameraCopy;
	public ComputeShader depthBufferDumperCS;

	void Start()
	{
		Camera.main.depthTextureMode = DepthTextureMode.DepthNormals;
	}

	void OnPostRender()
	{
		int width = Screen.width;
		int height = Screen.height;

		// color buffer
		{
			RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);

			cameraCopy.CopyFrom(Camera.main);
			cameraCopy.targetTexture = renderTexture;
			cameraCopy.backgroundColor = Color.gray;
			cameraCopy.Render();

			RenderTexture.active = renderTexture;
			Texture2D destTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
			destTexture.ReadPixels(new Rect(0.0f, 0.0f, width, height), 0, 0);
			Utils.SaveImage("Assets/dump_color_buffer.png", destTexture);

			RenderTexture.ReleaseTemporary(renderTexture);
			Destroy(destTexture);
		}

		// depth and normals buffers
		{
			RenderTextureDescriptor rtd_depth = new RenderTextureDescriptor(width, height, RenderTextureFormat.RFloat, 24)
			{
				enableRandomWrite = true
			};
			RenderTextureDescriptor rtd_normals = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 24)
			{
				enableRandomWrite = true
			};
			RenderTextureDescriptor rtd_pos = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGBFloat, 24)
			{
				enableRandomWrite = true
			};
			RenderTexture renderTexture_depth = RenderTexture.GetTemporary(rtd_depth);
			RenderTexture renderTexture_normals = RenderTexture.GetTemporary(rtd_normals);
			RenderTexture renderTexture_pos = RenderTexture.GetTemporary(rtd_pos);

			Texture depthTexture = Shader.GetGlobalTexture("_CameraDepthTexture");
			Texture depthNormalsTexture = Shader.GetGlobalTexture("_CameraDepthNormalsTexture");

			Matrix4x4 worldToProjMatrix = cameraCopy.projectionMatrix * cameraCopy.worldToCameraMatrix;
			Matrix4x4 projToWorldMatrix = worldToProjMatrix.inverse;

			depthBufferDumperCS.SetTexture(0, "_CameraDepthTexture", depthTexture);
			depthBufferDumperCS.SetTexture(0, "_CameraDepthNormalsTexture", depthNormalsTexture);
			depthBufferDumperCS.SetInt("_Width", width);
			depthBufferDumperCS.SetInt("_Height", height);
			depthBufferDumperCS.SetMatrix("_CameraToWorldMatrix", cameraCopy.cameraToWorldMatrix);
			depthBufferDumperCS.SetMatrix("_ProjToWorldMatrix", projToWorldMatrix);
			depthBufferDumperCS.SetTexture(0, "_ResultDepth", renderTexture_depth);
			depthBufferDumperCS.SetTexture(0, "_ResultNormals", renderTexture_normals);
			depthBufferDumperCS.SetTexture(0, "_ResultPos", renderTexture_pos);
			depthBufferDumperCS.Dispatch(0, width / 8 + 1, height / 8 + 1, 1);

			RenderTexture.active = renderTexture_depth;
			Texture2D destTexture_depth = new Texture2D(width, height, TextureFormat.RFloat, false, true);
			Texture2D destTexture_depth_rgba32 = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
			destTexture_depth.ReadPixels(new Rect(0.0f, 0.0f, width, height), 0, 0);
			NativeArray<float> destTexture_depth_rawData = destTexture_depth.GetRawTextureData<float>();
			// dump raw data
			{
				FileStream fs = File.Open("Assets/dump_depth_buffer.dat", FileMode.Create);
				BinaryWriter bw = new BinaryWriter(fs);

				bw.Write(width);
				bw.Write(height);
				NativeArray<byte> destTexture_depth_rawData_byte = destTexture_depth_rawData.Reinterpret<byte>(sizeof(float));
				bw.Write(destTexture_depth_rawData_byte.ToArray());

				bw.Close();
				fs.Close();
			}
			// dump debug-colored
			{
				Color32[] rgba = new Color32[width * height];
				for (int i = 0; i < width * height; i++)
				{
					byte value = (byte)Mathf.Clamp(255.0f * destTexture_depth_rawData[i], 0.0f, 255.0f);

					Color32 c = new Color32();
					c.r = value;
					c.g = value;
					c.b = value;
					c.a = 255;

					rgba[i] = c;
				}

				destTexture_depth_rgba32.SetPixels32(rgba);
				File.WriteAllBytes("Assets/dump_depth_buffer.png", destTexture_depth_rgba32.EncodeToPNG());
			}

			RenderTexture.active = renderTexture_normals;
			Texture2D destTexture_normals = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
			destTexture_normals.ReadPixels(new Rect(0.0f, 0.0f, width, height), 0, 0);
			Utils.SaveImage("Assets/dump_normals_buffer.png", destTexture_normals);

			RenderTexture.active = renderTexture_pos;
			Texture2D destTexture_pos = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true);
			Texture2D destTexture_pos_rgba32 = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
			destTexture_pos.ReadPixels(new Rect(0.0f, 0.0f, width, height), 0, 0);
			NativeArray<float> destTexture_pos_rawData = destTexture_pos.GetRawTextureData<float>();
			// dump raw data
			{
				FileStream fs = File.Open("Assets/dump_pos_buffer.dat", FileMode.Create);
				BinaryWriter bw = new BinaryWriter(fs);

				bw.Write(width);
				bw.Write(height);
				NativeArray<byte> destTexture_pos_rawData_byte = destTexture_pos_rawData.Reinterpret<byte>(sizeof(float));
				bw.Write(destTexture_pos_rawData_byte.ToArray());

				bw.Close();
				fs.Close();
			}
			// dump debug-colored
			{
				Color32[] rgba = new Color32[width * height];
				for (int i = 0; i < width * height; i++)
				{
					float x = destTexture_pos_rawData[4 * i + 0];
					float y = destTexture_pos_rawData[4 * i + 1];
					float z = destTexture_pos_rawData[4 * i + 2];

					x = x / 0.5f;
					y = y / 0.5f;
					z = z / 0.5f;
					x = Mathf.Clamp01(x);
					y = Mathf.Clamp01(y);
					z = Mathf.Clamp01(z);

					Color32 c = new Color32();
					c.r = (byte)Mathf.Clamp(255.0f * x, 0.0f, 255.0f);
					c.g = (byte)Mathf.Clamp(255.0f * y, 0.0f, 255.0f);
					c.b = (byte)Mathf.Clamp(255.0f * z, 0.0f, 255.0f);
					c.a = 255;

					rgba[i] = c;
				}

				destTexture_pos_rgba32.SetPixels32(rgba);
				File.WriteAllBytes("Assets/dump_pos_buffer.png", destTexture_pos_rgba32.EncodeToPNG());
			}

			RenderTexture.ReleaseTemporary(renderTexture_depth);
			RenderTexture.ReleaseTemporary(renderTexture_normals);
			RenderTexture.ReleaseTemporary(renderTexture_pos);
			Destroy(destTexture_depth);
			Destroy(destTexture_depth_rgba32);
			Destroy(destTexture_normals);
			Destroy(destTexture_pos);
			Destroy(destTexture_pos_rgba32);

			// how to load:
		//	float[,] depthBuffer = Utils.LoadImage_RawFloat("Assets/dump_depth_buffer.dat");
		//	Vector3[,] normalsBuffer = Utils.LoadImage_Normals("Assets/dump_normals_buffer.png");
		//	Vector4[,] posBuffer = Utils.LoadImage_RawFloat4("Assets/dump_pos_buffer.dat");
		//	width = depthBuffer.GetLength(0);
		//	height = depthBuffer.GetLength(1);
		}
	}
}
