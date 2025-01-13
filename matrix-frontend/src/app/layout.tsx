import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import "./globals.css";
import React from "react";

export const metadata: Metadata = {
  title: "Курсовая Декке Д.В.",
  description: "Упаковка матриц 4 схема",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="ru" data-theme="dark">
      <body>
        {children}
      </body>
    </html>
  );
}
