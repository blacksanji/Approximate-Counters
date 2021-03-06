﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace MemMap
{
    public class BLPTracker2
    {
        //components
        public MemCtrl2[] mctrls;
        public List<Bank2> banks;

        //states
        public int[] blp_perproc;

        //constructor
        public BLPTracker2(MemCtrl2[] mctrls)
        {
            this.mctrls = mctrls;

            //banks
            banks = new List<Bank2>();
            foreach (MemCtrl2 mc in this.mctrls) {
                Channel2 chan = mc.chan;
                for (uint r = 0; r < chan.rmax; r++) {
                    Rank2 rank = chan.ranks[r];
                    for (uint b = 0; b < rank.bmax; b++) {
                        Bank2 bank = rank.banks[b];
                        banks.Add(bank);
                    }
                }
            }

            //blp
            blp_perproc = new int[Config.N];
        }

        public void tick()
        {
            /* blp_perproc */
            Array.Clear(blp_perproc, 0, blp_perproc.Length);

            foreach (Bank2 b in banks) {
                Req req = get_curr_req(b);
                if (req == null) 
                    continue;

                blp_perproc[req.pid] += 1;
            }

            for (int pid = 0; pid < blp_perproc.Length; pid++) {
                int myblp = blp_perproc[pid];
                if (myblp == 0) 
                    continue;

                Stat.procs[pid].service_blp.Collect(myblp);
            }

            /* wblp */
            foreach (MemCtrl2 mctrl in mctrls) {
                if (!mctrl.wb_mode)
                    continue;

                int wbmode_blp = 0;
                for (uint r = 0; r < mctrl.rmax; r++) {
                    for (uint b = 0; b < mctrl.bmax; b++) {
                        Bank2 bank = mctrl.chan.ranks[r].banks[b];
                        Req req = get_curr_req(bank);
                        if (req == null)
                            continue;

                        wbmode_blp++;
                    }
                }
                Stat.mctrls2[mctrl.cid].wbmode_blp.Collect(wbmode_blp);
            }
        }

        public Req get_curr_req(Bank2 bank)
        {
            MemCtrl2 mc = bank.mc;
            List<Req> inflight_q = mc.inflightqs[bank.rid, bank.bid];
            if (inflight_q.Count == 0)
                return null;

            return inflight_q[inflight_q.Count - 1];
        }
    }
}
