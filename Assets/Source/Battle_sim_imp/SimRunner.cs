﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Battle_sim_imp
{
    #region Data structs
    public struct BlockData
    {
        public Vector2 pos;
        public BlockData(Block block)
        {
            pos = block.pos;
        }
    }

    public struct WarrData
    {
        public Vector2 pos;
        public WarrData(Warrior warr)
        {
            pos = warr.BlockThisAt.pos;
        }
    }

    public struct BodyData
    {
        public Vector2 pos;
        public BodyData(Body body)
        {
            pos = body.pos;
        }
    }
    #endregion

    public class SimRunner
    {
        Battle_sim_imp.Sim sim;
        public bool isRunning = false;
        public int fps = 100;

        public List<BlockData> blockPoss;
        public List<WarrData> warrPoss;
        public List<BodyData> bodyPoss;
        public object exportDataLock = new object();

        public SimRunner(int width, int height)
        {
            blockPoss = new List<BlockData>();
            warrPoss = new List<WarrData>();
            bodyPoss = new List<BodyData>();
            sim = new Battle_sim_imp.Sim(height, width);
        }

        public void StartLoop()
        {
            var time = Environment.TickCount;
            while (isRunning)
            {
                Debug.Log(String.Format("{0} {1} {2}", Thread.CurrentThread.IsBackground, Thread.CurrentThread.ManagedThreadId, Thread.CurrentThread.ThreadState));
                try
                {
                    if (Environment.TickCount - time > 1000 / fps)
                    {
                        lock (exportDataLock)
                        {
                            sim.ExportData(blockPoss, warrPoss, bodyPoss);
                        }
                        time = Environment.TickCount;
                    }
                }
                catch(Exception e)
                {
                    Debug.Log(e);
                }
            }
        }
    }
}