'use client'
import React, {useEffect, useState} from 'react';
import {createCompletedRoot} from "@/app/Shared/Helpers/FetchHelper";
import {func} from "ts-interface-checker";

export default function MatrixChangeValue({id, values, pointers, bandWidth, handleMatrixChange}: {
    id: string,
    values: number[],
    pointers: number[],
    bandWidth: number,
    handleMatrixChange: () => void
}) {
    const [row, setRow] = useState('');
    const [col, setCol] = useState('');
    const [newValue, setNewValue] = useState('');
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');
    useEffect(() => {
        if (row && row !== '' && col && col !== '') {
            setNewValue(findElementInPackedMatrix().toString())
        }
    }, [row, col]);

    function findElementInPackedMatrix(): number {
        const nrow = parseInt(row);
        const ncol = parseInt(col);
        // Проверяем, если разница между строкой и столбцом больше ширины ленты
        if (Math.abs(nrow - ncol) > bandWidth) {
            return 0; // Элемент за пределами ленты
        }

        // Если строка больше столбца, то ищем элемент в нижней треугольной части (или диагонали)
        if (nrow > ncol) {
            // Индекс для элементов в верхней треугольной части матрицы
            const indexInValues = pointers[nrow] - (nrow - ncol);

            return values[indexInValues]; // Возвращаем элемент, если он найден
        }

        // Если строка меньше или равна столбцу, то ищем элемент в верхней треугольной части
        if (nrow <= ncol) {
            const indexInValues = pointers[ncol] - (ncol - nrow);

            return values[indexInValues]; // Возвращаем элемент, если он найден
        }

        return 0; // Если элемент не найден
    }

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();

        setLoading(true);
        setError('');

        const payload = { row: row, col: col, newValue: newValue };

        try {
            // Создаем строку параметров
            const queryParams = new URLSearchParams(payload).toString();

            // Добавляем параметры в URL
            const url = createCompletedRoot(`/MatrixPacking/ChangeMatrixElementInPackedForm/${id}?${queryParams}`);

            // Выполняем POST-запрос (тело не нужно, параметры в query)
            const response = await fetch(url, {
                method: 'POST',
            });

            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(errorText);
            }

            // Вызов коллбека, чтобы родительский компонент обновил данные

            alert('Элемент матрицы успешно изменен!');
            handleMatrixChange();
        } catch (error: any) {
            setError(error.message || 'Произошла ошибка при изменении элемента.');
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="p-6">
            <h2 className="text-lg font-semibold text-primary mb-4">Изменить элемент матрицы</h2>
            <form onSubmit={handleSubmit}>
                <div className="flex flex-wrap items-end gap-4 mb-4">
                    <label className="form-control w-full max-w-xs">
                        <div className="label">
                            <span className="label-text">Номер строки</span>
                        </div>
                        <input
                            type="number"
                            placeholder="Строка"
                            value={row}
                            onChange={(e) => setRow(e.target.value)}
                            className="input input-sm input-bordered w-full max-w-xs"
                        />
                    </label>
                    <label className="form-control w-full max-w-xs">
                        <div className="label">
                            <span className="label-text">Номер столбца</span>
                        </div>
                        <input
                            type="number"
                            placeholder="Столбец"
                            value={col}
                            onChange={(e) => setCol(e.target.value)}
                            className="input input-sm  input-bordered w-full max-w-xs"
                        />
                    </label>
                    <label className="form-control w-full max-w-xs">
                        <div className="label">
                            <span className="label-text">Значение в ячейке</span>
                        </div>
                        <input
                            type="number"
                            placeholder="Новое значение"
                            value={newValue}
                            onChange={(e) => setNewValue(e.target.value)}
                            className="input input-sm input-bordered w-full max-w-xs"
                        />
                    </label>
                        <button type="submit" className="btn btn-sm btn-primary" disabled={loading}>
                            {loading ? 'Обновление...' : 'Изменить элемент'}
                        </button>
                </div>
            </form>
            {error && <p className="text-red-500 mt-4">{error}</p>}
        </div>
);
}
