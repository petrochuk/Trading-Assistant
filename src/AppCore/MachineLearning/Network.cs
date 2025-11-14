using System.Diagnostics.CodeAnalysis;

namespace AppCore.MachineLearning;

public class Network
{
    private const int FileFormatVersion = 1;
    private readonly int _inputSize;
    private readonly int _outputSize;
    private readonly int _hiddenLayers;
    private readonly int _hiddenSize;
    private readonly double _learningRate;
    
    private double[][][] _weights; // [layer][neuron][weight]
    private double[][] _biases;    // [layer][neuron]
    private double[][] _activations; // [layer][neuron]
    private double[][] _zValues;   // [layer][neuron] - values before activation
    
    private readonly Random _random;

    [SetsRequiredMembers]
    public Network(int inputSize, int outputSize, int hiddenLayers, int hiddenSize, double learningRate = 0.5)
    {
        _inputSize = inputSize;
        _outputSize = outputSize;
        _hiddenLayers = hiddenLayers;
        _hiddenSize = hiddenSize;
        _learningRate = learningRate;
        _random = new Random(42); // Fixed seed for reproducibility

        InitializeNetwork();
    }

    private void InitializeNetwork()
    {
        int totalLayers = _hiddenLayers + 1; // hidden layers + output layer
        
        _weights = new double[totalLayers][][];
        _biases = new double[totalLayers][];
        _activations = new double[totalLayers + 1][]; // +1 for input layer
        _zValues = new double[totalLayers][];

        // Initialize input layer activations
        _activations[0] = new double[_inputSize];

        // Initialize hidden layers
        for (int layer = 0; layer < _hiddenLayers; layer++)
        {
            int prevLayerSize = layer == 0 ? _inputSize : _hiddenSize;
            
            _weights[layer] = new double[_hiddenSize][];
            _biases[layer] = new double[_hiddenSize];
            _activations[layer + 1] = new double[_hiddenSize];
            _zValues[layer] = new double[_hiddenSize];

            for (int neuron = 0; neuron < _hiddenSize; neuron++)
            {
                _weights[layer][neuron] = new double[prevLayerSize];
                _biases[layer][neuron] = _random.NextDouble() * 2 - 1; // Random between -1 and 1

                for (int weight = 0; weight < prevLayerSize; weight++)
                {
                    _weights[layer][neuron][weight] = _random.NextDouble() * 2 - 1;
                }
            }
        }

        // Initialize output layer
        int outputLayerIndex = _hiddenLayers;
        int prevSize = _hiddenLayers > 0 ? _hiddenSize : _inputSize;
        
        _weights[outputLayerIndex] = new double[_outputSize][];
        _biases[outputLayerIndex] = new double[_outputSize];
        _activations[outputLayerIndex + 1] = new double[_outputSize];
        _zValues[outputLayerIndex] = new double[_outputSize];

        for (int neuron = 0; neuron < _outputSize; neuron++)
        {
            _weights[outputLayerIndex][neuron] = new double[prevSize];
            _biases[outputLayerIndex][neuron] = _random.NextDouble() * 2 - 1;

            for (int weight = 0; weight < prevSize; weight++)
            {
                _weights[outputLayerIndex][neuron][weight] = _random.NextDouble() * 2 - 1;
            }
        }
    }

    public int InputSize => _inputSize;

    public double[] Forward(double[] inputs)
    {
        if (inputs.Length != _inputSize)
            throw new ArgumentException($"Input size must be {_inputSize}");

        // Set input activations
        Array.Copy(inputs, _activations[0], _inputSize);

        // Forward propagation through all layers
        for (int layer = 0; layer <= _hiddenLayers; layer++)
        {
            double[] prevActivations = _activations[layer];
            int currentLayerSize = layer == _hiddenLayers ? _outputSize : _hiddenSize;

            for (int neuron = 0; neuron < currentLayerSize; neuron++)
            {
                double sum = _biases[layer][neuron];
                
                for (int prevNeuron = 0; prevNeuron < prevActivations.Length; prevNeuron++)
                {
                    sum += prevActivations[prevNeuron] * _weights[layer][neuron][prevNeuron];
                }

                _zValues[layer][neuron] = sum;
                _activations[layer + 1][neuron] = Sigmoid(sum);
            }
        }

        // Return output layer activations
        double[] outputs = new double[_outputSize];
        Array.Copy(_activations[_hiddenLayers + 1], outputs, _outputSize);
        return outputs;
    }

    public void Backward(double[] expectedOutputs)
    {
        if (expectedOutputs.Length != _outputSize)
            throw new ArgumentException($"Expected output size must be {_outputSize}");

        // Calculate gradients for each layer (starting from output layer)
        double[][] deltas = new double[_hiddenLayers + 1][];

        // Output layer deltas
        int outputLayerIndex = _hiddenLayers;
        deltas[outputLayerIndex] = new double[_outputSize];
        
        for (int neuron = 0; neuron < _outputSize; neuron++)
        {
            double output = _activations[outputLayerIndex + 1][neuron];
            double error = output - expectedOutputs[neuron];
            deltas[outputLayerIndex][neuron] = error * SigmoidDerivative(_zValues[outputLayerIndex][neuron]);
        }

        // Hidden layer deltas (backpropagation)
        for (int layer = _hiddenLayers - 1; layer >= 0; layer--)
        {
            deltas[layer] = new double[_hiddenSize];
            
            for (int neuron = 0; neuron < _hiddenSize; neuron++)
            {
                double error = 0;
                int nextLayerSize = layer == _hiddenLayers - 1 ? _outputSize : _hiddenSize;
                
                for (int nextNeuron = 0; nextNeuron < nextLayerSize; nextNeuron++)
                {
                    error += deltas[layer + 1][nextNeuron] * _weights[layer + 1][nextNeuron][neuron];
                }
                
                deltas[layer][neuron] = error * SigmoidDerivative(_zValues[layer][neuron]);
            }
        }

        // Update weights and biases
        for (int layer = 0; layer <= _hiddenLayers; layer++)
        {
            double[] prevActivations = _activations[layer];
            int currentLayerSize = layer == _hiddenLayers ? _outputSize : _hiddenSize;

            for (int neuron = 0; neuron < currentLayerSize; neuron++)
            {
                // Update bias
                _biases[layer][neuron] -= _learningRate * deltas[layer][neuron];

                // Update weights
                for (int prevNeuron = 0; prevNeuron < prevActivations.Length; prevNeuron++)
                {
                    _weights[layer][neuron][prevNeuron] -= _learningRate * deltas[layer][neuron] * prevActivations[prevNeuron];
                }
            }
        }
    }

    internal double Train(double[] inputs, double output) {
        if (inputs.Length != _inputSize)
            throw new ArgumentException($"Input size must be {_inputSize}");
        if (_outputSize != 1)
            throw new InvalidOperationException("This Train method only supports single output networks.");

        var outputs = Forward(inputs);
        Backward([output]);

        return outputs[0] - output;
    }

    public void Train(double[][] inputs, double[][] expectedOutputs, int epochs)
    {
        if (inputs.Length != expectedOutputs.Length)
            throw new ArgumentException("Input and output arrays must have the same length");

        for (int epoch = 0; epoch < epochs; epoch++)
        {
            double totalError = 0;

            for (int sample = 0; sample < inputs.Length; sample++)
            {
                double[] outputs = Forward(inputs[sample]);
                Backward(expectedOutputs[sample]);

                // Calculate error for this sample
                for (int i = 0; i < outputs.Length; i++)
                {
                    double error = outputs[i] - expectedOutputs[sample][i];
                    totalError += error * error;
                }
            }

            // Print progress every 1000 epochs
            if (epoch % 1000 == 0)
            {
                double avgError = totalError / (inputs.Length * _outputSize);
                Console.WriteLine($"Epoch {epoch}, Average Error: {avgError:F6}");
            }
        }
    }

    public double[] Predict(double[] inputs)
    {
        return Forward(inputs);
    }

    private static double Sigmoid(double x)
    {
        return 1.0 / (1.0 + System.Math.Exp(-x));
    }

    private static double SigmoidDerivative(double x)
    {
        double sigmoid = Sigmoid(x);
        return sigmoid * (1 - sigmoid);
    }

    /// <summary>
    /// Saves the neural network to a binary file.
    /// </summary>
    /// <param name="filePath">The path to the file where the network will be saved.</param>
    public void Save(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(stream);

        // Write file format version
        writer.Write(FileFormatVersion);

        // Write network architecture
        writer.Write(_inputSize);
        writer.Write(_outputSize);
        writer.Write(_hiddenLayers);
        writer.Write(_hiddenSize);
        writer.Write(_learningRate);

        // Write weights
        for (int layer = 0; layer < _weights.Length; layer++)
        {
            writer.Write(_weights[layer].Length);
            for (int neuron = 0; neuron < _weights[layer].Length; neuron++)
            {
                writer.Write(_weights[layer][neuron].Length);
                for (int weight = 0; weight < _weights[layer][neuron].Length; weight++)
                {
                    writer.Write(_weights[layer][neuron][weight]);
                }
            }
        }

        // Write biases
        for (int layer = 0; layer < _biases.Length; layer++)
        {
            writer.Write(_biases[layer].Length);
            for (int neuron = 0; neuron < _biases[layer].Length; neuron++)
            {
                writer.Write(_biases[layer][neuron]);
            }
        }
    }

    /// <summary>
    /// Loads a neural network from a binary file.
    /// </summary>
    /// <param name="filePath">The path to the file from which to load the network.</param>
    /// <returns>A new Network instance with the loaded parameters.</returns>
    public static Network Load(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(stream);

        // Read and validate file format version
        int version = reader.ReadInt32();
        if (version != FileFormatVersion)
        {
            throw new InvalidOperationException($"Unsupported file format version: {version}. Expected version: {FileFormatVersion}");
        }

        // Read network architecture
        int inputSize = reader.ReadInt32();
        int outputSize = reader.ReadInt32();
        int hiddenLayers = reader.ReadInt32();
        int hiddenSize = reader.ReadInt32();
        double learningRate = reader.ReadDouble();

        // Create network with the loaded architecture
        var network = new Network(inputSize, outputSize, hiddenLayers, hiddenSize, learningRate);

        // Read weights
        for (int layer = 0; layer < network._weights.Length; layer++)
        {
            int neuronCount = reader.ReadInt32();
            if (neuronCount != network._weights[layer].Length)
            {
                throw new InvalidOperationException($"Weight structure mismatch at layer {layer}");
            }

            for (int neuron = 0; neuron < neuronCount; neuron++)
            {
                int weightCount = reader.ReadInt32();
                if (weightCount != network._weights[layer][neuron].Length)
                {
                    throw new InvalidOperationException($"Weight count mismatch at layer {layer}, neuron {neuron}");
                }

                for (int weight = 0; weight < weightCount; weight++)
                {
                    network._weights[layer][neuron][weight] = reader.ReadDouble();
                }
            }
        }

        // Read biases
        for (int layer = 0; layer < network._biases.Length; layer++)
        {
            int neuronCount = reader.ReadInt32();
            if (neuronCount != network._biases[layer].Length)
            {
                throw new InvalidOperationException($"Bias structure mismatch at layer {layer}");
            }

            for (int neuron = 0; neuron < neuronCount; neuron++)
            {
                network._biases[layer][neuron] = reader.ReadDouble();
            }
        }

        return network;
    }
}