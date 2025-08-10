// Copyright SkyComb Limited 2025. All rights reserved.

namespace SkyCombDrone.Exceptions
{
    /// <summary>
    /// Base class for all drone data processing exceptions
    /// </summary>
    public abstract class DroneDataException : Exception
    {
        protected DroneDataException(string message) : base(message) { }
        protected DroneDataException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when a video format is not supported
    /// </summary>
    public class UnsupportedVideoFormatException : DroneDataException
    {
        /// <summary>
        /// Gets the unsupported file path
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// Initializes a new instance of UnsupportedVideoFormatException
        /// </summary>
        /// <param name="filePath">The path to the unsupported video file</param>
        /// <param name="message">The exception message</param>
        public UnsupportedVideoFormatException(string filePath, string message) : base(message)
        {
            FilePath = filePath;
        }

        /// <summary>
        /// Initializes a new instance of UnsupportedVideoFormatException
        /// </summary>
        /// <param name="filePath">The path to the unsupported video file</param>
        /// <param name="message">The exception message</param>
        /// <param name="innerException">The inner exception</param>
        public UnsupportedVideoFormatException(string filePath, string message, Exception innerException) : base(message, innerException)
        {
            FilePath = filePath;
        }
    }

    /// <summary>
    /// Exception thrown when drone flight log data cannot be processed
    /// </summary>
    public class FlightLogProcessingException : DroneDataException
    {
        /// <summary>
        /// Gets the flight log file path if applicable
        /// </summary>
        public string? LogFilePath { get; }

        /// <summary>
        /// Initializes a new instance of FlightLogProcessingException
        /// </summary>
        /// <param name="message">The exception message</param>
        public FlightLogProcessingException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of FlightLogProcessingException
        /// </summary>
        /// <param name="logFilePath">The path to the problematic log file</param>
        /// <param name="message">The exception message</param>
        public FlightLogProcessingException(string logFilePath, string message) : base(message)
        {
            LogFilePath = logFilePath;
        }

        /// <summary>
        /// Initializes a new instance of FlightLogProcessingException
        /// </summary>
        /// <param name="logFilePath">The path to the problematic log file</param>
        /// <param name="message">The exception message</param>
        /// <param name="innerException">The inner exception</param>
        public FlightLogProcessingException(string logFilePath, string message, Exception innerException) : base(message, innerException)
        {
            LogFilePath = logFilePath;
        }
    }

    /// <summary>
    /// Exception thrown when drone data processing encounters insufficient data
    /// </summary>
    public class InsufficientDroneDataException : DroneDataException
    {
        /// <summary>
        /// Gets the data type that was insufficient
        /// </summary>
        public string DataType { get; }

        /// <summary>
        /// Initializes a new instance of InsufficientDroneDataException
        /// </summary>
        /// <param name="dataType">The type of data that was insufficient</param>
        /// <param name="message">The exception message</param>
        public InsufficientDroneDataException(string dataType, string message) : base(message)
        {
            DataType = dataType;
        }

        /// <summary>
        /// Initializes a new instance of InsufficientDroneDataException
        /// </summary>
        /// <param name="dataType">The type of data that was insufficient</param>
        /// <param name="message">The exception message</param>
        /// <param name="innerException">The inner exception</param>
        public InsufficientDroneDataException(string dataType, string message, Exception innerException) : base(message, innerException)
        {
            DataType = dataType;
        }
    }

    /// <summary>
    /// Exception thrown when drone camera calibration data is invalid or missing
    /// </summary>
    public class CameraCalibrationException : DroneDataException
    {
        /// <summary>
        /// Gets the camera type or identifier
        /// </summary>
        public string? CameraType { get; }

        /// <summary>
        /// Initializes a new instance of CameraCalibrationException
        /// </summary>
        /// <param name="message">The exception message</param>
        public CameraCalibrationException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of CameraCalibrationException
        /// </summary>
        /// <param name="cameraType">The camera type or identifier</param>
        /// <param name="message">The exception message</param>
        public CameraCalibrationException(string cameraType, string message) : base(message)
        {
            CameraType = cameraType;
        }

        /// <summary>
        /// Initializes a new instance of CameraCalibrationException
        /// </summary>
        /// <param name="cameraType">The camera type or identifier</param>
        /// <param name="message">The exception message</param>
        /// <param name="innerException">The inner exception</param>
        public CameraCalibrationException(string cameraType, string message, Exception innerException) : base(message, innerException)
        {
            CameraType = cameraType;
        }
    }

    /// <summary>
    /// Exception thrown when drone metadata cannot be processed
    /// </summary>
    public class DroneMetadataException : DroneDataException
    {
        /// <summary>
        /// Gets the metadata field that caused the issue
        /// </summary>
        public string? MetadataField { get; }

        /// <summary>
        /// Initializes a new instance of DroneMetadataException
        /// </summary>
        /// <param name="message">The exception message</param>
        public DroneMetadataException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of DroneMetadataException
        /// </summary>
        /// <param name="metadataField">The metadata field that caused the issue</param>
        /// <param name="message">The exception message</param>
        public DroneMetadataException(string metadataField, string message) : base(message)
        {
            MetadataField = metadataField;
        }

        /// <summary>
        /// Initializes a new instance of DroneMetadataException
        /// </summary>
        /// <param name="metadataField">The metadata field that caused the issue</param>
        /// <param name="message">The exception message</param>
        /// <param name="innerException">The inner exception</param>
        public DroneMetadataException(string metadataField, string message, Exception innerException) : base(message, innerException)
        {
            MetadataField = metadataField;
        }
    }
}