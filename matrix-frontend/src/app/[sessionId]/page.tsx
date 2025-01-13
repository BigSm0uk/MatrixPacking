import { createCompletedRoot } from "@/app/Shared/Helpers/FetchHelper";
import ClientMatrix from "@/app/[sessionId]/ClientMatrix";

export default async function Page({ params }: { params: { sessionId: string } }) {
    const sessionId = (await params).sessionId;

    const response = await fetch(createCompletedRoot(`/MatrixPacking/GetResultMatrix?id=${sessionId}`));

    if (!response.ok) {
        const errorText = await response.text();
        console.error(`Ошибка загрузки данных: ${response.status} - ${errorText}`);
        return <div>Произошла ошибка: {errorText}</div>;
    }

    const packedMatrix = await response.json();

    return (
        <div className="p-6">
            <div className="card shadow-lg bg-base-200">
                <div className="card-body">
                    <h1 className="card-title text-primary text-2xl">Результаты сессии</h1>
                    <p className="text-gray-500">
                        <span className="font-bold">ID сессии:</span> {sessionId}
                    </p>
                    <div className="divider"></div>
                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                        <div>
                            <p className="text-lg">
                                <span className="font-semibold text-secondary">Ширина ленты:</span> {packedMatrix.bandWidth} элементов
                            </p>
                            <p className="text-lg">
                                <span className="font-semibold text-secondary">Упакованный размер:</span> {packedMatrix.packedSize} элементов
                            </p>
                            <p className="text-lg">
                                <span className="font-semibold text-secondary">Общий размер матрицы:</span> {packedMatrix.totalMatrixSize} элементов
                            </p>
                        </div>
                        <div className="grid grid-cols-2 gap-4">
                            <ClientMatrix title="Значения матрицы (упакованы)" data={packedMatrix.values} />
                            <ClientMatrix title="Указатели матрицы" data={packedMatrix.pointers} />
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
}
