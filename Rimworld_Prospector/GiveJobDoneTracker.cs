﻿using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Rimworld_Prospector
{
    public class GiveJobDoneTracker
    {
        private readonly Dictionary<string, JobDoneWrapper> Tracker;

        public GiveJobDoneTracker()
        {
            Tracker = new Dictionary<string, JobDoneWrapper>();
        }

        public void AddJob(Thing pawn, Job job)
        {
            var key = GetKey(pawn, job);
            Tracker.Add(key, new JobDoneWrapper(job));
        }

        public void RemoveJob(Thing pawn, Job job)
        {
            var key = GetKey(pawn, job);
            Tracker.Remove(key);
        }

        public bool IsDone(Thing pawn, Job job)
        {
            var key = GetKey(pawn, job);
            return Tracker.ContainsKey(key) && Tracker[key].IsDone;
        }

        public bool SetDone(Thing pawn, Job job)
        {
            var key = GetKey(pawn, job);
            if (!Tracker.ContainsKey(key))
            {
                return false;
            }

            Tracker[key].IsDone = true;
            return true;
        }

        private static string GetKey(Thing pawn, Job job)
        {
            return pawn.ThingID + job.GetUniqueLoadID();
        }
        
        private class JobDoneWrapper
        {
            private Job Job { get; }
            public bool IsDone { get; set; }
            
            public JobDoneWrapper(Job job)
            {
                Job = job;
            }
        }
    }
}