export const createCompletedRoot = (path: string) =>{
    return process.env.NEXT_PUBLIC_API_BASE_URL + path;
}
export const downloadFile =  (blob : Blob) => {
    try {
        const link = document.createElement('a');
        link.href = URL.createObjectURL(blob);
        link.download = 'Результаты.xlsx';  // Имя файла, которое будет сохранено
        link.click();
    } catch (error) {
        console.error('Ошибка при скачивании файла:', error);
    }
};
