using System;
using System.IO;
using FluentAssertions;
using DistributedSolver.Core.Models;
using DistributedSolver.Core.Services;
using Xunit;

namespace DistributedSolver.Tests.UnitTests;

public class NodeFileReaderTests
{
    private static string CreateTempFile(string content)
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, content);
        return tempFile;
    }

    [Fact]
    public void NodeFileReader_ReadNodesFromFile_ShouldReadValidFile()
    {
        var filePath = CreateTempFile("localhost:5000\nlocalhost:5001\n192.168.1.1:8080");

        try
        {
            var nodes = NodeFileReader.ReadNodesFromFile(filePath);

            nodes.Should().HaveCount(3);
            nodes[0].Address.Should().Be("localhost");
            nodes[0].Port.Should().Be(5000);
            nodes[2].Address.Should().Be("192.168.1.1");
            nodes[2].Port.Should().Be(8080);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void NodeFileReader_ReadNodesFromFile_ShouldIgnoreCommentsAndEmptyLines()
    {
        var filePath = CreateTempFile("# comment\nlocalhost:5000\n\nlocalhost:5001");

        try
        {
            var nodes = NodeFileReader.ReadNodesFromFile(filePath);
            nodes.Should().HaveCount(2);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void NodeFileReader_ReadNodesFromFile_ShouldThrowOnInvalidFormat()
    {
        var filePath = CreateTempFile("localhost:5000\ninvalid");

        try
        {
            Assert.Throws<FormatException>(() => NodeFileReader.ReadNodesFromFile(filePath));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void NodeInfo_ToString_ShouldReturnCorrectFormat()
    {
        var node = new NodeInfo("localhost", 5000);
        node.ToString().Should().Be("localhost:5000");
        node.FullUrl.Should().Be("http://localhost:5000");
    }
}


