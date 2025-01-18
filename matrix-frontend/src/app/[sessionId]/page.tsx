'use client'
import {useState, useEffect} from 'react';
import {createCompletedRoot, downloadFile} from "@/app/Shared/Helpers/FetchHelper";
import ClientMatrix from "@/app/[sessionId]/ClientMatrix";
import MatrixChangeValue from "@/app/[sessionId]/MatrixChangeValue";

// Wrap your server-side params into use() to resolve the Promise
export default function Page({params}: { params: Promise<{ sessionId: string }> }) {
    const [sessionId, setSessionId] = useState<string | null>(null);
    const [packedMatrix, setPackedMatrix] = useState<any>(null);
    const [error, setError] = useState<string | null>(null);
    useEffect(() => {
        const fetchSessionId = async () => {
            // Wait for the params Promise to resolve
            const {sessionId} = await params;
            setSessionId(sessionId);
        };

        fetchSessionId();
    }, [params]);
    const fetchMatrixData = async () => {
        try {
            const response = await fetch(createCompletedRoot(`/MatrixPacking/GetResultMatrix/${sessionId}`));

            if (!response.ok) {
                const errorText = await response.text();
                setError(`Ошибка загрузки данных: ${response.status} - ${errorText}`);
                return;
            }

            const data = await response.json();
            setPackedMatrix(data);
        } catch (err) {
            setError('Ошибка при загрузке данных');
            console.error(err);
        }
    };
    useEffect(() => {
        if (sessionId) {
            fetchMatrixData();
        }
    }, [sessionId]);

    if (error) {
        return <div>{error}</div>;
    }

    if (!packedMatrix) {
        return <div>Загрузка данных...</div>;
    }

    const handleFileLoad = async () => {
        try {
            // Формируем URL с параметром sessionId
            const response = await fetch(createCompletedRoot(`/MatrixPacking/GetResultMatrixFile/${sessionId}`));

            // Проверяем, что ответ успешен
            if (response.ok) {
                let blob = await response.blob()
                downloadFile(blob);
            } else {
                // В случае ошибки на сервере
                console.error("Ошибка загрузки файла:", response.statusText);
            }
        } catch (error) {
            console.error("Ошибка запроса:", error);
        }
    }
    function handleMatrixChange(){
        fetchMatrixData();
    }
    return (
        <div className="p-6">
            <div className="card shadow-lg bg-base-200">
                <div className="card-body">
                    <h1 className="card-title text-primary text-2xl">Результаты сессии <button
                        className="btn btn-sm btn-link w-20 text-primary" onClick={handleFileLoad}>
                        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" width="24" height="24"
                             fill="currentColor">
                            <path
                                d="m15,7V0H5c-1.657,0-3,1.343-3,3v21h20V7h-7Zm-1.594,12.417c-.388.388-.897.581-1.406.581s-1.019-.193-1.406-.581l-3.299-3.299,1.414-1.414,2.291,2.291v-5.997h2v6.008l2.291-2.302,1.414,1.414-3.299,3.299Zm8.008-14.417h-4.414V.586l4.414,4.414Z"/>
                        </svg>
                    </button></h1>
                    <p className="text-gray-500">
                        <span className="font-bold">ID сессии:</span> {sessionId}
                    </p>

                    <div className="divider"></div>
                    <MatrixChangeValue handleMatrixChangeAction={handleMatrixChange} bandWidth={packedMatrix.maxBandWidth} values={packedMatrix.values}
                                       pointers={packedMatrix.pointers} id={sessionId!}></MatrixChangeValue>
                    <div className="divider"></div>
                    {/* Секция статистики */}
                    <div className="stats stats-vertical lg:stats-horizontal shadow mb-4">
                        <div className="stat place-items-center">
                            <div className="stat-title">Максимальная ширина ленты</div>
                            <div className="stat-value text-primary">{packedMatrix.maxBandWidth}</div>
                            <div className="stat-desc">Количество элементов</div>
                        </div>
                        <div className="stat place-items-center">
                            <div className="stat-title">Упакованный размер</div>
                            <div className="stat-value text-secondary">{packedMatrix.packedSize}</div>
                            <div className="stat-desc">Количество элементов</div>
                        </div>
                        <div className="stat place-items-center">
                            <div className="stat-title">Общий размер матрицы</div>
                            <div className="stat-value">{packedMatrix.totalMatrixSize}</div>
                            <div className="stat-desc">Количество элементов</div>
                        </div>
                    </div>

                    {/* Остальная часть контента */}
                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                        <ClientMatrix title="Значения упакованной матрицы"
                                      data={packedMatrix.values}/>
                        <ClientMatrix title="Указатели матрицы" data={packedMatrix.pointers}/>
                    </div>
                </div>
            </div>
        </div>
    );
}
