﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace MemMap
{
    public class OrCoupledIdleOnsetBusyEnd : MemWBMode
    {
        public uint min_wb_window;
        public uint max_wb_window;
        public uint wb_mode_cycles;

        private bool rmpkis_valid = false;
        private double[] rmpkis;
        private bool[] is_low_rmpki;
        private uint low_rmpki_cnt;

        public OrCoupledIdleOnsetBusyEnd(MemCtrl[] mctrls)
            : base(mctrls)
        {
            min_wb_window = Config.mctrl.min_wb_window;
            max_wb_window = Config.mctrl.max_wb_window;

            rmpkis = new double[Config.N];
            is_low_rmpki = new bool[Config.N];
        }

        private void calculate_rmpki()
        {
            low_rmpki_cnt = 0;

            for (int pid = 0; pid < Config.N; pid++) {
                ulong read_cnt = Stat.procs[pid].read_req.Count;
                ulong inst_cnt = Stat.procs[pid].ipc.Count;
                double rmpki = 1000 * ((double)read_cnt) / inst_cnt;

                rmpkis[pid] = rmpki;
                is_low_rmpki[pid] = (rmpki < Config.mctrl.low_rmpki_threshold);
                if (is_low_rmpki[pid])
                    low_rmpki_cnt++;
            }

            if (!rmpkis_valid)
                rmpkis_valid = true;
        }

        private bool is_idle(uint cid)
        {
            if (!rmpkis_valid)
                return false;

            if (low_rmpki_cnt == 0)
                return false;

            MemCtrl mctrl = mctrls[cid];
            if (mctrl.wload < 0.25 * mctrl.mctrl_writeq.Capacity)
                return false;

            for (int pid = 0; pid < Config.N; pid++) {
                if (!is_low_rmpki[pid]) continue;

                if (mctrl.rload_per_proc[pid] > 0)
                    return false;
            }

            return true;
        }

        private bool is_busy(uint cid)
        {
            if (!rmpkis_valid)
                return false;

            if (low_rmpki_cnt == 0)
                return false;

            MemCtrl mctrl = mctrls[cid];
            for (int pid = 0; pid < Config.N; pid++) {
                if (!is_low_rmpki[pid]) continue;

                if (mctrl.rload_per_proc[pid] > 0)
                    return true;
            }

            return false;
        }

        public override void tick(uint cid)
        {
            if (cid != 0) return;

            cycles++;

            //calculate rmpki;
            if (cycles % 10000 == 0) {
                calculate_rmpki();
            }

            //check for end of wb_mode
            if (wb_mode[0]) {
                wb_mode_cycles++;

                bool is_end = false;

                if (min_wb_window <= wb_mode_cycles && wb_mode_cycles < max_wb_window) {
                    bool any_busy = false;
                    for (uint i = 0; i < cmax; i++) {
                        any_busy = any_busy || is_busy(i);
                    }

                    is_end = any_busy;
                }
                else if (wb_mode_cycles == max_wb_window) {
                    is_end = true;
                }

                if (is_end) {
                    for (uint i = 0; i < cmax; i++) {
                        wb_mode[i] = false;
                    }
                }
            }

            //check for start of wb_mode
            if (wb_mode[0])
                return;

            bool any_writeq_full = false;
            bool any_readq_empty = false;
            bool any_idle = false;

            for (uint i = 0; i < cmax; i++) {
                any_writeq_full = any_writeq_full || is_writeq_full(i);
                any_readq_empty = any_readq_empty || is_readq_empty(i);
                any_idle = any_idle || is_idle(i);
            }

            if (any_writeq_full || any_readq_empty || any_idle) {
                for (uint i = 0; i < cmax; i++) {
                    wb_mode[i] = true;
                }
                wb_mode_cycles = 0;
            }
        }
    }
}
