'use client';

import React, {useEffect, useState} from 'react';
import {useForm, Controller} from 'react-hook-form';
import {createCompletedRoot} from "@/app/Shared/Helpers/FetchHelper";

export default function MatrixChangeValue({
                                              id,
                                              values,
                                              pointers,
                                              bandWidth,
                                              handleMatrixChangeAction,
                                          }: {
    id: string;
    values: number[];
    pointers: number[];
    bandWidth: number;
    handleMatrixChangeAction: () => void;
}) {
    const {control, handleSubmit, watch, setValue, formState: {errors, isValid}} = useForm({
        mode: 'onChange', // Обновление валидности формы при изменении полей
        defaultValues: {
            row: '',
            col: '',
            newValue: '',
        },
    });

    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');

    const row = watch('row');
    const col = watch('col');

    useEffect(() => {
        if (row && col) {
            setValue('newValue', findElementInPackedMatrix().toString());
        }
    }, [row, col, setValue]);

    function findElementInPackedMatrix(): number {
        let nrow = parseInt(row || '0');
        let ncol = parseInt(col || '0');

        if (Math.abs(nrow - ncol) > bandWidth || nrow < 0 || ncol < 0) {
            return 0; // Элемент за пределами ленты
        }
        if (nrow === ncol && nrow === 0) return values[0];
        if (ncol > nrow) {
            let temp = nrow;
            nrow = ncol;
            ncol = temp;
        }
        const localBandWidth = pointers[nrow] - pointers[nrow - 1] - 1;
        if (nrow - ncol > localBandWidth ) return 0;

        const indexInValues = pointers[nrow] - (nrow - ncol);

        return values[indexInValues] || 0; // Возвращаем элемент, если он найден
    }

    const onSubmit = async (data: any) => {
        setLoading(true);
        setError('');

        const payload = {row: data.row, col: data.col, newValue: data.newValue};

        try {
            const queryParams = new URLSearchParams(payload).toString();
            const url = createCompletedRoot(`/MatrixPacking/ChangeMatrixElementInPackedForm/${id}?${queryParams}`);

            const response = await fetch(url, {method: 'POST'});
            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(errorText);
            }

            alert('Элемент матрицы успешно изменен!');
            handleMatrixChangeAction();
        } catch (err: any) {
            setError(err.message || 'Произошла ошибка при изменении элемента.');
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="p-6">
            <h2 className="text-lg font-semibold text-primary mb-4">Изменить элемент матрицы</h2>
            <form onSubmit={handleSubmit(onSubmit)}>
                <div className="flex flex-wrap items-center gap-4 mb-4">
                    <Controller
                        name="row"
                        control={control}
                        rules={{
                            required: 'Введите номер строки',
                            min: {value: 0, message: 'Значение должно быть в пределах индексов матрицы'},
                            max: {
                                value: pointers.length - 1,
                                message: 'Значение должно быть в пределах индексов матрицы '
                            }
                        }}
                        render={({field}) => (
                            <label className="form-control h-32 w-full max-w-xs">
                                <div className="label">
                                    <span className="label-text">Номер строки</span>
                                </div>
                                <input
                                    type="number"
                                    placeholder="Строка"
                                    className={`input input-sm input-bordered w-full max-w-xs ${errors.row ? 'input-error' : ''}`}
                                    {...field}
                                />
                                {errors.row && <p className="text-red-500 text-sm">{errors.row.message}</p>}
                            </label>
                        )}
                    />
                    <Controller
                        name="col"
                        control={control}
                        rules={{
                            required: 'Введите номер столбца',
                            min: {value: 0, message: 'Значение должно быть в пределах индексов матрицы'},
                            max: {
                                value: pointers.length - 1,
                                message: 'Значение должно быть в пределах индексов матрицы '
                            }
                        }}
                        render={({field}) => (
                            <label className="form-control h-32 w-full max-w-xs">
                                <div className="label">
                                    <span className="label-text">Номер столбца</span>
                                </div>
                                <input
                                    type="number"
                                    placeholder="Столбец"
                                    className={`input input-sm input-bordered w-full max-w-xs ${errors.col ? 'input-error' : ''}`}
                                    {...field}
                                />
                                {errors.col && <p className="text-red-500 text-sm">{errors.col.message}</p>}
                            </label>
                        )}
                    />
                    <Controller
                        name="newValue"
                        control={control}
                        rules={{required: 'Введите значение'}}
                        render={({field}) => (
                            <label className="form-control h-32 w-full max-w-xs">
                                <div className="label">
                                    <span className="label-text">Значение в ячейке</span>
                                </div>
                                <input
                                    type="number"
                                    placeholder="Новое значение"
                                    className={`input input-sm input-bordered w-full max-w-xs ${errors.newValue ? 'input-error' : ''}`}
                                    {...field}
                                />
                                {errors.newValue && <p className="text-red-500 text-sm">{errors.newValue.message}</p>}
                            </label>
                        )}
                    />
                    <button type="submit" className="btn btn-sm btn-primary" disabled={!isValid || loading}>
                        {loading ? 'Обновление...' : 'Изменить элемент'}
                    </button>
                </div>
            </form>
            {error && <p className="text-red-500 mt-4">{error}</p>}
        </div>
    );
}
