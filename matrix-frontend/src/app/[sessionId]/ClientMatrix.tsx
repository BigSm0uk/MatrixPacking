'use client';

import React from "react";
import Image from "next/image";

export default function ClientMatrix({ title, data }: { title: string; data: number[] }) {
    const handleCopy = () => {
        const textToCopy = data.join(', ');
        navigator.clipboard.writeText(textToCopy).then(() => {
        });
    };

    return (
        <div className="flex flex-col">
            <div className="flex justify-between items-center">
                <h2 className="text-lg font-bold">{title}</h2>
                <button className="btn btn-sm btn-primary" onClick={handleCopy}>
                    <Image src="/copy.png" width={20} height={20} alt="Копировать"></Image>
                </button>
            </div>
            <div
                className="p-2 bg-base-100 max-h-[300px] rounded border border-base-300 text-sm overflow-auto"
            >
                {data.join(', ')}
            </div>
        </div>
    );
}
