'use client'
import React from "react";
import {Controller, useForm} from "react-hook-form";
import {createCompletedRoot} from "@/app/Shared/Helpers/FetchHelper";
import {useRouter} from "next/navigation";
import getConfig from "next/config";

export default function Home() {
    const router = useRouter()
    const {control, watch, handleSubmit} = useForm({
        defaultValues: {
            useTestFile: false,
            file: null,
        },
    });
    const onSubmit = async (data: { useTestFile: boolean; file: File | null }) => {
        let fileToUpload = null;

        if (data.useTestFile) {
            // Загружаем файл из папки public
            try {
                const response = await fetch("/matrix/Данные_приложение_3.xlsx"); // Укажите путь к вашему тестовому файлу
                const blob = await response.blob();
                fileToUpload = new File([blob], "Данные_приложение_3.xlsx", {
                    type: blob.type,
                });
            } catch (error) {
                alert("Не удалось загрузить тестовый файл");
                return;
            }
        } else if (data.file) {
            fileToUpload = data.file;
        }

        if (fileToUpload) {
            const formData = new FormData();
            formData.append("file", fileToUpload);

            // Отправка файла на сервер
            try {
                // Отправляем запрос на создание сессии
                const idResponse = await fetch(createCompletedRoot("/MatrixPacking/CreatePackingSession"), {
                    method: "POST",
                    body: formData,
                });

                // Проверяем, успешен ли запрос
                if (!idResponse.ok) {
                    alert(`Ошибка при создании сессии: ${idResponse.status} ${idResponse.statusText}`);
                }

                // Парсим ответ
                const result = await idResponse.json();

                // Проверяем, есть ли sessionId в ответе
                if (!result?.sessionId) {
                    alert("Ответ не содержит sessionId");
                }
                router.push(`${process.env.NEXT_PUBLIC_API_BASE_PATH}/${result.sessionId}`);
                
            } catch (error : any) {
                console.error("Произошла ошибка:", error.message);
            }
        }
    };
    // Наблюдаем за состоянием чекбокса и файла
    const useTestFile = watch("useTestFile");
    const file = watch("file");

    return (
        <div className="flex items-center justify-center min-h-screen">
            <div className="card w-full h-auto lg:w-2/3 shadow-xl rounded-lg">
                <div className="card-body p-6 lg:p-8">
                    <h2 className="card-title text-lg lg:text-2xl font-semibold mb-4">
                        Загрузите файл исходных данных
                    </h2>
                    <p className="mb-2">
                        Файл должен быть формата <kbd className="kbd kbd-sm">.xlsx</kbd>
                    </p>
                    <p className="mb-6">
                        Пример файла можно скачать
                        <a className="link link-primary ml-1" href="/matrix/Данные_приложение_3.xlsx">
                            по ссылке
                        </a>
                    </p>
                    <div className="card-actions">
                        <form
                            onSubmit={handleSubmit(onSubmit)}
                            className="flex w-full flex-col items-center space-y-4"
                        >
                            <Controller
                                name="useTestFile"
                                control={control}
                                render={({field}) => (
                                    <label className="flex items-center space-x-2">
                                        <input
                                            disabled={!!file}
                                            type="checkbox"
                                            className="checkbox checkbox-primary"
                                            checked={field.value} // Управление состоянием чекбокса
                                            onChange={(e) => field.onChange(e.target.checked)} // Передача значения `boolean`
                                        />
                                        <span>Использовать тестовый файл</span>
                                    </label>
                                )}
                            />
                            <Controller
                                name="file"
                                control={control}
                                render={({field}) => (
                                    <input
                                        disabled={!!useTestFile}
                                        type="file"
                                        className="file-input file-input-bordered file-input-primary w-full max-w-xs"
                                        accept=".xlsx"
                                        onChange={(e) =>
                                            field.onChange(
                                                e.target.files ? e.target.files[0] : null
                                            )
                                        }
                                    />
                                )}
                            />
                            <button
                                type="submit"
                                className="btn btn-primary w-full max-w-xs"
                                disabled={!useTestFile && !file}
                            >
                                Продолжить
                            </button>
                        </form>
                    </div>
                </div>
            </div>
        </div>
    );
}
