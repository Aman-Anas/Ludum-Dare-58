using System;
using Godot;
using Godot.Collections;

public static class TestComputeShader
{
    static void RunTest()
    {
        // Create a local rendering device.
        var renderDevice = RenderingServer.CreateLocalRenderingDevice();
        var rd = renderDevice;
        // Load GLSL shader
        var shaderFile = GD.Load<RDShaderFile>("res://scripts/terrain/TestCompute.glsl");
        var shaderBytecode = shaderFile.GetSpirV();
        var shader = rd.ShaderCreateFromSpirV(shaderBytecode);

        // Prepare our data. We use floats in the shader, so we need 32 bit.
        var input = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var inputBytes = new byte[input.Length * sizeof(int)];
        Buffer.BlockCopy(input, 0, inputBytes, 0, inputBytes.Length);

        var counterInput = new uint[] { 0 };
        var counterInBytes = new byte[counterInput.Length * sizeof(uint)];
        Buffer.BlockCopy(counterInput, 0, counterInBytes, 0, counterInBytes.Length);

        // Create a storage buffer that can hold our float values.
        // Each float has 4 bytes (32 bit) so 10 x 4 = 40 bytes
        var buffer = rd.StorageBufferCreate((uint)inputBytes.Length, inputBytes);

        // Create a uniform to assign the buffer to the rendering device
        var uniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.StorageBuffer,
            Binding = 0
        };
        uniform.AddId(buffer);

        // Rid countBuffer;
        var countBuffer = InitStorageBuffer(
            renderDevice,
            out var countUniform,
            (uint)counterInBytes.Length,
            1,
            counterInBytes
        );

        var uniformSet = rd.UniformSetCreate(
            new Array<RDUniform> { uniform, countUniform },
            shader,
            0
        );

        // Create a compute pipeline
        var pipeline = rd.ComputePipelineCreate(shader);
        var computeList = rd.ComputeListBegin();
        rd.ComputeListBindComputePipeline(computeList, pipeline);
        rd.ComputeListBindUniformSet(computeList, uniformSet, 0);
        rd.ComputeListDispatch(computeList, xGroups: 2, yGroups: 1, zGroups: 1);
        rd.ComputeListEnd();

        // Submit to GPU and wait for sync
        rd.Submit();
        rd.Sync();

        // Read back the data from the buffers
        var outputBytes = rd.BufferGetData(buffer);
        var output = new int[input.Length];
        Buffer.BlockCopy(outputBytes, 0, output, 0, outputBytes.Length);

        var outputCounterBytes = rd.BufferGetData(countBuffer);
        var outCount = new uint[counterInput.Length];
        Buffer.BlockCopy(outputCounterBytes, 0, outCount, 0, outputCounterBytes.Length);

        GD.Print("Input: ", string.Join(", ", input));
        GD.Print("Output: ", string.Join(", ", output));

        GD.Print("Input: ", string.Join(", ", counterInput));
        GD.Print("Output: ", string.Join(", ", outCount));
    }

    // Helper function to auto setup a storage buffer
    static Rid InitStorageBuffer(
        RenderingDevice renderDevice,
        out RDUniform uniform,
        uint size,
        int bindIndex,
        byte[] data = null
    )
    {
        var newBuffer = renderDevice.StorageBufferCreate(size, data);
        var newUniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.StorageBuffer,
            Binding = bindIndex
        };
        newUniform.AddId(newBuffer);
        uniform = newUniform;
        return newBuffer;
    }
}
