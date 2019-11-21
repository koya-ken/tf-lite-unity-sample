﻿using System.IO;
using UnityEngine;

namespace TensorFlowLite
{
    public class SSD : System.IDisposable
    {
        public struct Result
        {
            public int classID;
            public float score;
            public Rect rect;
        }

        const int WIDTH = 300;
        const int HEIGHT = 300;
        const int CHANNELS = 3; // RGB

        Interpreter interpreter;
        ComputeShader compute;
        ComputeBuffer inputBuffer;
        RenderTexture resizeTexture;
        Material resizeMat;

        // https://www.tensorflow.org/lite/models/object_detection/overview
        uint[] inputInts = new uint[WIDTH * HEIGHT * CHANNELS];
        sbyte[] inputBytes = new sbyte[WIDTH * HEIGHT * CHANNELS];

        float[] outputs0 = new float[10 * 4]; // [top, left, bottom, right] * 10
        float[] outputs1 = new float[10]; // Classes
        float[] outputs2 = new float[10]; // Scores
        Result[] results = new Result[10];

        public SSD(string modelPath, ComputeShader compute)
        {
            this.compute = compute;

            interpreter = new Interpreter(File.ReadAllBytes(modelPath), 2);
            interpreter.ResizeInputTensor(0, new int[] { 1, HEIGHT, WIDTH, CHANNELS });
            interpreter.AllocateTensors();

            inputBuffer = new ComputeBuffer(WIDTH * HEIGHT * CHANNELS, sizeof(uint)); // uint8

        }

        public void Dispose()
        {
            interpreter?.Dispose();
            inputBuffer?.Dispose();

            resizeTexture?.Release();
        }

        public void Invoke(Texture inputTex)
        {
            RenderTexture tex = ResizeTexture(inputTex);

            // TextureToBytesGPU(tex, inputBytes);
            TextureToBytesCPU(tex, inputBytes);

            Invoke(inputBytes);
        }

        RenderTexture ResizeTexture(Texture texture)
        {
            if (resizeTexture == null)
            {
                resizeTexture = new RenderTexture(300, 300, 0, RenderTextureFormat.ARGB32);
                resizeMat = new Material(Shader.Find("Hidden/YFlip"));

                resizeMat.SetInt("_FlipX", Application.isMobilePlatform ? 1 : 0);
                resizeMat.SetInt("_FlipY", 1);
            }
            Graphics.Blit(texture, resizeTexture, resizeMat, 0);
            return resizeTexture;
        }

        void TextureToBytesGPU(RenderTexture texture, sbyte[] inputs)
        {
            compute.SetTexture(0, "InputTexture", texture);
            compute.SetBuffer(0, "OutputTensor", inputBuffer);
            compute.Dispatch(0, WIDTH / 10, HEIGHT / 10, 1);

            // Note:
            // ComputeShader doesn't support byte quantize
            // Therefore receive as uint, then convert to sbyte
            inputBuffer.GetData(inputInts);
            for (int i = 0; i < inputInts.Length; i++)
            {
                inputs[i] = (sbyte)inputInts[i];
            }
        }

        Texture2D fetchTexture;
        void TextureToBytesCPU(RenderTexture texture, sbyte[] inputs)
        {
            if (fetchTexture == null)
            {
                fetchTexture = new Texture2D(WIDTH, HEIGHT, TextureFormat.RGB24, 0, false);
            }

            var prevRT = RenderTexture.active;
            RenderTexture.active = texture;

            fetchTexture.ReadPixels(new Rect(0, 0, WIDTH, HEIGHT), 0, 0);
            fetchTexture.Apply();

            RenderTexture.active = prevRT;

            // TODO invert Y
            var pixels = fetchTexture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                inputs[i * 3] = unchecked((sbyte)pixels[i].r);
                inputs[i * 3 + 1] = unchecked((sbyte)pixels[i].g);
                inputs[i * 3 + 2] = unchecked((sbyte)pixels[i].b);
            }
        }


        void Invoke(sbyte[] inputs)
        {
            interpreter.SetInputTensorData(0, inputs);
            interpreter.Invoke();
            interpreter.GetOutputTensorData(0, outputs0);
            interpreter.GetOutputTensorData(1, outputs1);
            interpreter.GetOutputTensorData(2, outputs2);
        }

        public Result[] GetResults()
        {
            for (int i = 0; i < 10; i++)
            {
                // Invert Y to adapt Unity UI space
                float top = 1f - outputs0[i * 4];
                float left = outputs0[i * 4 + 1];
                float bottom = 1f - outputs0[i * 4 + 2];
                float right = outputs0[i * 4 + 3];

                results[i] = new Result()
                {
                    classID = (int)outputs1[i],
                    score = outputs2[i],
                    rect = new Rect(left, top, right - left, top - bottom),
                };
            }
            return results;
        }
    }
}