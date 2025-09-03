using System;
using System.Collections.Generic;

namespace DeviceProgramming.Memory
{
    /// <summary>
    /// This class represents a contiguous memory layout that consists of consecutive blocks.
    /// </summary>
    public class Layout
    {
        public UInt64 StartAddress 
        { 
            get 
            {
                if (Blocks.Count == 0)
                {
                    return 0;
                }
                else
                {
                    return Blocks[0].StartAddress;
                }
            }
        }
        public UInt64 EndAddress { get { return StartAddress + Size - 1; } }
        public UInt64 Size { get; protected set; }
        public List<Block> Blocks { get; protected set; }

        public Layout()
        {
            Size = 0;
            Blocks = new List<Block>();
        }

        /// <summary>
        /// Appends a new block at the end of the layout.
        /// </summary>
        /// <param name="newBlock">The new last block</param>
        public void AppendBlock(Block newBlock)
        {
            if (Blocks.Count > 0)
            {
                if ((EndAddress + 1) != newBlock.StartAddress)
                {
                    throw new ArgumentException("New block creates inconsistent memory layout.", "newBlock");
                }
            }

            Blocks.Add(newBlock);
            Size += newBlock.Size;
        }
    }
}
