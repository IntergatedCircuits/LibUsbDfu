using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibUsbDfu
{
    public enum StandardRequest : byte
    {
        GetStatus = 0x00,           /// Get current status of features
        ClearFeature = 0x01,        /// Clear the activation of a feature
        SetFeature = 0x03,          /// Activation of a feature
        SetAddress = 0x05,          /// Set the bus address of the device
        GetDescriptor = 0x06,       /// Get a descriptor from the device
        SetDescriptor = 0x07,       /// Write a descriptor in the device
        GetConfiguration = 0x08,    /// Get the current device configuration index
        SetConfiguration = 0x09,    /// Set the new device configuration index
        GetInterface = 0x0A,        /// Get the current alternate setting of the interface
        SetInterface = 0x0B,        /// Set the new alternate setting of the interface
        SynchFrame = 0x0C,
    }
}
