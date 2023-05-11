using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Goodtech.Testing
{
    public class TimeCalc
    {
        

        public List<Sample> Samples;
        private double AverageDiff => GetCalcAverage();
        private double Deviation => GetCalcDeviation();


        public TimeCalc()
        {
            Samples = new List<Sample>();
        }

        private double GetCalcAverage()
        {
            if (Samples.Count == 0) return 0;
            Sample[] copy = Samples.ToArray();
            //var sum = copy.Sum(sample => sample.diff);
            
            // ReSharper disable once PossibleLossOfFraction
            var sum = Samples.Average(sample => sample.diff);
            return sum;
        }


        private double GetCalcDeviation()
        {
            if (Samples.Count == 0) return 0;
            var avg = GetCalcAverage();
            var sumOfSquares = Samples.Sum(sample => (sample.diff - avg) * (sample.diff - avg));
            var variance = sumOfSquares / (Samples.Count - 1);
            return Math.Sqrt(variance);
        }

        public override string ToString()
        {
            GetCalcDeviation();
            return $"{nameof(AverageDiff)}: {AverageDiff}, {nameof(Deviation)}: {Deviation}, Samples Count: {Samples.Count}";
        }

        public void LogResults(string path)
        {
            string log = "";
            foreach (var sample in Samples)
            {
                log += sample.ToString();
                log += "\n";
            }
            log += ToString();
            var newPath = path.Substring(0, path.LastIndexOf('.'));
            newPath += DateTime.Now.ToString("yyyyMMddHHmmss");
            newPath += ".txt";
            File.WriteAllText(newPath, log);
        }
    }
    public class Sample
    {
        /// <summary>
        /// The internal value for transmitted/outgoing time.
        /// </summary>
        private long _tx;
        /// <summary>
        /// Name of the sample.
        /// </summary>
        public string name;
        /// <summary>
        /// The difference between <see cref="tx"/> and <see cref="rx"/>
        /// </summary>
        public long diff;
        /// <summary>
        /// Rx = Received. The time the sample was created, or when we want to start measuring time.
        /// </summary>
        public long rx;
        /// <summary>
        /// Tx = Transmitted. The time when the sample was closed, or when we want to stop measuring time.
        /// </summary>
        public long tx
        {
            get => _tx;
            set
            {
                _tx = value; 
                diff = value >= rx ? (value - rx) : (value + (1000 - rx));
            }
        }
        public override string ToString()
        {
            return $"{nameof(rx)}: {rx},\t {nameof(tx)}: {tx},\t {nameof(diff)}:\t{diff},\t {nameof(name)}: {name}";
        }
    }
}