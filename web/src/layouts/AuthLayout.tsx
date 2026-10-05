import React from 'react';
import { Outlet } from 'react-router-dom';
import ecoBinIcon from '../assets/Eco Recycling Bin Icon.png';
import loginBg from '../assets/waste login background.png';

export const AuthLayout: React.FC = () => {
  return (
    <div className="relative min-h-screen flex items-center justify-center bg-slate-950 px-4 py-8 sm:px-6 lg:px-8 overflow-hidden">
      {/* Background Image */}
      <img
        src={loginBg}
        alt="Smart Waste Background"
        className="absolute inset-0 w-full h-full object-cover object-center select-none pointer-events-none"
      />
      {/* Modern Professional Login Card */}
      <div className="relative z-10 w-full max-w-md bg-white/95 backdrop-blur-md rounded-2xl shadow-2xl shadow-slate-950/40 border border-white/60 p-6 sm:p-8">
        <div className="text-center mb-6">
          <div className="flex justify-center -mt-2 -mb-1">
            <img
              src={ecoBinIcon}
              alt="Bin It Eco Recycling Bin"
              className="w-24 h-24 sm:w-28 sm:h-28 object-contain drop-shadow-md select-none pointer-events-none"
            />
          </div>
          <h1 className="text-3xl sm:text-[34px] font-black tracking-tight text-slate-900">
            Bin <span className="text-emerald-600">It</span>
          </h1>
          <p className="text-sm text-slate-500 mt-1 font-medium">
            Clean, efficient municipal waste operations
          </p>
        </div>
        <Outlet />
      </div>
    </div>
  );
};

