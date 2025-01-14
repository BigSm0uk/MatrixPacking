'use client'
import React, {useEffect, useState} from 'react';
import {createCompletedRoot} from "@/app/Shared/Helpers/FetchHelper";
import {func} from "ts-interface-checker";

export default function MatrixChangeValue({id, values, pointers, bandWidth}: { id: string, values: number[], pointers: number[], bandWidth: number }) {
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
            const startColumn = Math.max(0, nrow - bandWidth);
            const indexInValues = pointers[nrow] - (nrow - ncol);

            return values[indexInValues]; // Возвращаем элемент, если он найден
        }

        // Если строка меньше или равна столбцу, то ищем элемент в верхней треугольной части
        if (nrow <= ncol) {
            const startColumn = Math.max(0, ncol - bandWidth); // Определяем диапазон столбцов в пределах ленты
            const indexInValues = pointers[ncol] - (ncol - nrow - startColumn);

            return values[indexInValues]; // Возвращаем элемент, если он найден
        }

        return 0; // Если элемент не найден
    }

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();

        setLoading(true);
        setError('');

        const payload = {row: parseInt(row), col: parseInt(col), newValue: parseInt(newValue)};

        try {
            const response = await fetch(createCompletedRoot(`/MatrixPacking/ChangeMatrixElementInPackedForm/${id}`), {
                method: 'POST',
                headers: {'Content-Type': 'application/json'},
                body: JSON.stringify(payload),
            });

            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(errorText);
            }

            // Вызов коллбека, чтобы родительский компонент обновил данные

            alert('Элемент матрицы успешно изменен!');
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
                <div className="flex gap-4 mb-4">
                    <input
                        type="number"
                        placeholder="Строка"
                        value={row}
                        onChange={(e) => setRow(e.target.value)}
                        className="input input-sm input-bordered w-full max-w-xs"
                    />
                    <input
                        type="number"
                        placeholder="Столбец"
                        value={col}
                        onChange={(e) => setCol(e.target.value)}
                        className="input input-sm  input-bordered w-full max-w-xs"
                    />
                    <input
                        type="number"
                        placeholder="Новое значение"
                        value={newValue}
                        onChange={(e) => setNewValue(e.target.value)}
                        className="input input-sm input-bordered w-full max-w-xs"
                    />
                </div>
                <button type="submit" className="btn btn-sm btn-primary w-full" disabled={loading}>
                    {loading ? 'Обновление...' : 'Изменить элемент'}
                </button>
            </form>
            {error && <p className="text-red-500 mt-4">{error}</p>}
        </div>
    );
}
