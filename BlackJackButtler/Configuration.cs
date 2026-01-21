using System;
using Dalamud.Configuration;

namespace BlackJackButtler;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
  public int Version { get; set; } = 0;

  public List<MessageBatch> MessageBatches { get; set; } = new();

  public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}

[Serializable]
public sealed class MessageBatch
{
  public string Name { get; set; } = "New Batch";
  public bool IsExpanded { get; set; } = true;

  public List<string> Messages { get; set; } = new() { "Hello!", "Another line..." };
}
