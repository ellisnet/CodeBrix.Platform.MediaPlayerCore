using System;
using System.Linq;
using System.Threading.Tasks;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Webcam.Tests;

public class WebcamDevicesTests
{
    // These tests make NO assumption that a camera is connected — they verify the
    // enumeration contract for whatever is (or is not) present.

    [Fact]
    public async Task Device_list_is_never_null()
    {
        var devices = await WebcamDevices.GetImagingMediaDeviceListAsync();
        devices.Should().NotBeNull();
    }

    [Fact]
    public async Task Every_enumerated_device_is_fully_populated()
    {
        var devices = await WebcamDevices.GetImagingMediaDeviceListAsync();
        foreach (var device in devices)
        {
            device.Id.Should().NotBeNullOrEmpty();
            device.FriendlyName.Should().NotBeNullOrEmpty();
            device.Hardware.Should().NotBeNull();
            device.Capabilities.Should().NotBeNull();
            device.Controls.Should().NotBeNull();

            foreach (var capability in device.Capabilities)
            {
                capability.FourCc.Should().NotBeNullOrEmpty();
                capability.Width.Should().BeGreaterThan((uint)0);
                capability.Height.Should().BeGreaterThan((uint)0);
                capability.FrameRates.Should().NotBeNull();
            }

            foreach (var control in device.Controls)
            {
                control.Name.Should().NotBeNullOrEmpty();
                control.Maximum.Should().BeGreaterThanOrEqualTo(control.Minimum);
            }
        }
    }

    [Fact]
    public async Task Enumerated_device_ids_are_distinct()
    {
        var devices = await WebcamDevices.GetImagingMediaDeviceListAsync();
        devices.Select(d => d.Id).Distinct().Count().Should().Be(devices.Count);
    }
}
