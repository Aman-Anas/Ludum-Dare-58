#[compute]
#version 460

// Invocations in the (x, y, z) dimension
layout(local_size_x = 10, local_size_y = 1, local_size_z = 1) in;

// A binding to the buffer we create in our script
layout(set = 0, binding = 0, std430) restrict buffer MyDataBuffer {
    int data[];
} my_data_buffer;

layout(set = 0, binding = 1, std430) coherent buffer Counter {
    uint counter;
};


// The code we want to execute in each invocation
void main() {
    // gl_GlobalInvocationID.x uniquely identifies this invocation across all work groups
    // my_data_buffer.data[gl_GlobalInvocationID.x] += 1 ;//gl_GlobalInvocationID.x;//*= 2.0;
    uint index = atomicAdd(counter, 1u);
    atomicAdd(my_data_buffer.data[gl_LocalInvocationID.x], 1);
}